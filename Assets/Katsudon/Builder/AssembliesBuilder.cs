using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Katsudon.Builder.Ctor;
using Katsudon.Builder.Helpers;
using Katsudon.Builder.Methods;
using Katsudon.Builder.Variables;
using Katsudon.Info;
using Katsudon.Meta;
using Katsudon.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using VRC.Udon.ProgramSources;

namespace Katsudon.Builder
{
	public class AssembliesBuilder : IModulesContainer, IDisposable
	{
		public const string ASSEMBLIES_META_FILE = "assembliesMeta";

		private AssembliesInfo assembliesInfo;
		private MethodBodyBuilder methodBodyBuilder;
		private BehaviourMethodBuilder behaviourMethodBuilder;
		private VariableBuildersCollection variableBuilders;
		private CtorDefaultsExtractor defaultsExtractor;
		private StringBuilder cachedSb = new StringBuilder();
		private PrimitiveConvertersList convertersList;

		private List<UdonMethodMeta> metaCache = new List<UdonMethodMeta>();
		private UdonAssembliesMetaWriter metaWriter;

		private Dictionary<Type, object> modules = new Dictionary<Type, object>();
		private List<TypeOpCodeBuider> typeOperationBuilders;

		public AssembliesBuilder()
		{
			assembliesInfo = AssembliesInfo.instance;
			AddModule(assembliesInfo);

			var sortedStaticModules = OrderedTypeUtils.GetOrderedSet<StaticBuilderModuleAttribute>();
			var ctorArgs = new object[] { this };
			foreach(var pair in sortedStaticModules)
			{
				var method = MethodSearch<ModuleConstructorDelegate>.FindStaticMethod(pair.Value, "Register");
				Assert.IsNotNull(method, string.Format("Static module with type {0} does not have a Register method", pair.Value));
				method.Invoke(null, ctorArgs);
			}

			convertersList = new PrimitiveConvertersList(this);

			methodBodyBuilder = new MethodBodyBuilder();
			AddModule(methodBodyBuilder);

			behaviourMethodBuilder = new BehaviourMethodBuilder(methodBodyBuilder, convertersList);

			var sortedOperationBuilders = OrderedTypeUtils.GetOrderedSet<OperationBuilderAttribute>();
			var builderArgs = new object[] { methodBodyBuilder, this };
			foreach(var pair in sortedOperationBuilders)
			{
				var method = MethodSearch<OperationBuilderDelegate>.FindStaticMethod(pair.Value, "Register");
				Assert.IsNotNull(method, string.Format("Operation builder with type {0} does not have a Register method", pair.Value));
				method.Invoke(null, builderArgs);
			}

			var sortedTypeBuilders = new SortedSet<TypeOpCodeBuider>(new TypeOpCodeBuider());
			var typeBuilders = AttribCollectUtils.CollectTypes<TypeOperationBuilderAttribute>(false);
			for(int i = 0; i < typeBuilders.Length; i++)
			{
				var register = MethodSearch<OperationBuilderDelegate>.FindStaticMethod(typeBuilders[i].Value, "Register");
				Assert.IsNotNull(register, string.Format("Operation builder with type {0} does not have a Register method", typeBuilders[i].Value));
				var unRegister = MethodSearch<OperationBuilderDelegate>.FindStaticMethod(typeBuilders[i].Value, "UnRegister");
				Assert.IsNotNull(unRegister, string.Format("Operation builder with type {0} does not have a UnRegister method", typeBuilders[i].Value));
				sortedTypeBuilders.Add(new TypeOpCodeBuider(register, unRegister, typeBuilders[i].Key.registerOrder, typeBuilders[i].Value));
			}
			typeOperationBuilders = new List<TypeOpCodeBuider>(sortedTypeBuilders.Count);
			typeOperationBuilders.AddRange(sortedTypeBuilders);

			defaultsExtractor = new CtorDefaultsExtractor();

			variableBuilders = new VariableBuildersCollection(this);

			metaWriter = new UdonAssembliesMetaWriter(FileUtils.GetWriteStream(ASSEMBLIES_META_FILE));
		}

		public void Dispose()
		{
			metaWriter.Flush();
			metaWriter.Dispose();
		}

		public T GetModule<T>() where T : class
		{
			if(modules.TryGetValue(typeof(T), out var obj) && obj is T module) return module;
			return null;
		}

		public void AddModule<T>(T instance) where T : class
		{
			modules.Add(typeof(T), instance);
		}

		public void RemoveModule<T>() where T : class
		{
			modules.Remove(typeof(T));
		}

		public void BuildClass(Type classType, string programPath, int executionOrder)
		{
			//prepare
			var classInfo = assembliesInfo.GetTypeInfo(classType);
			var methods = new Dictionary<MethodIdentifier, AsmMethodInfo>();
			classInfo.CollectMethods(methods);
			var typeHierarhy = new List<Type>();

			var interfaceMethodsMap = new Dictionary<MethodInfo, MethodInfo>();
			for(Type t = classType; t != typeof(MonoBehaviour); t = t.BaseType)
			{
				typeHierarhy.Add(t);
				MapInterfaceMethods(t.GetInterfaces(), t, interfaceMethodsMap);
			}

			List<Guid> inheritedFromGuids = new List<Guid>();
			for(Type t = classType; t != typeof(MonoBehaviour); t = t.BaseType)
			{
				if(t != classType) inheritedFromGuids.Add(assembliesInfo.GetTypeInfo(t).guid);
				var interfaces = t.GetInterfaces();
				for(var i = 0; i < interfaces.Length; i++)
				{
					inheritedFromGuids.Add(assembliesInfo.GetTypeInfo(interfaces[i]).guid);
				}
			}

			var buildOrder = new List<MethodInfo>();
			var buildMethods = new HashSet<MethodInfo>();

			foreach(var pair in methods)
			{
				if((pair.Value.flags & AsmMethodInfo.Flags.Export) != 0)
				{
					var method = pair.Value.method;
					if(buildMethods.Add(method)) buildOrder.Add(method);
				}
			}

			foreach(var pair in interfaceMethodsMap)
			{
				if(!buildMethods.Contains(pair.Key))
				{
					buildMethods.Add(pair.Key);
					buildOrder.Add(pair.Key);
				}
			}

			// build
			var methodsCollection = new MethodsInstance(classInfo, buildOrder, buildMethods);
			foreach(var item in interfaceMethodsMap)
			{
				methodsCollection.GetByInfo(new AsmMethodInfo(AsmMethodInfo.Flags.Unique | AsmMethodInfo.Flags.Export,
					assembliesInfo.GetMethod(item.Key.DeclaringType, item.Key), item.Key));
			}

			var variableNamesCounter = new Dictionary<string, int>();
			classInfo.CollectFieldCounters(variableNamesCounter);

			var builder = new UdonProgramBuilder();
			var propertiesBlock = new PropertiesBlock(variableBuilders, variableNamesCounter);
			builder.AddBlock(propertiesBlock);
			propertiesBlock.AddVariable(new AddressedSignificantVariable(AsmTypeInfo.TYPE_ID_NAME, typeof(Guid), 0x00, classInfo.guid));
			inheritedFromGuids.Sort();
			propertiesBlock.AddVariable(new AddressedSignificantVariable(AsmTypeInfo.INHERIT_IDS_NAME, typeof(Guid[]), 0x01, inheritedFromGuids.ToArray()));

			var fieldsCollection = new FieldsCollection(classInfo);
			var constCollection = new ConstCollection();
			var externsCollection = new ExternsCollection();

			AddModule(classInfo);
			AddModule(fieldsCollection);
			AddModule(methodsCollection);
			for(int i = 0; i < typeOperationBuilders.Count; i++)
			{
				typeOperationBuilders[i].Register(methodBodyBuilder, this);
			}

			metaCache.Clear();
			var machine = new UdonMachine(metaCache, classInfo, constCollection, externsCollection, fieldsCollection);
			var programBlock = new ProgramBlock(machine, propertiesBlock, executionOrder);
			programBlock.AddMethodBuilder(behaviourMethodBuilder);
			programBlock.AddMethodBuilder(new InterfaceMethodBuilder(interfaceMethodsMap, methodBodyBuilder, convertersList, methodsCollection));
			builder.AddBlock(programBlock);

			Action<MethodInfo> addMethodCallback = (method) => {
				if(buildMethods.Add(method)) buildOrder.Add(method);
			};

			classInfo.onMethodRequested += addMethodCallback;
			for(var i = 0; i < buildOrder.Count; i++)
			{
				programBlock.BuildMethod(buildOrder[i], methodsCollection.GetDirect(buildOrder[i]));
			}
			classInfo.onMethodRequested -= addMethodCallback;

			for(int i = typeHierarhy.Count - 1; i >= 0; i--)
			{
				var ctor = typeHierarhy[i].GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
				if(ctor != null)
				{
					defaultsExtractor.ExtractDefaults(ctor, fieldsCollection);
				}
			}

			programBlock.BuildMachine();

			fieldsCollection.Apply(propertiesBlock);
			constCollection.Apply(propertiesBlock);
			externsCollection.Apply(propertiesBlock);

			cachedSb.Clear();
			var program = builder.Build(cachedSb);

			for(int i = typeOperationBuilders.Count - 1; i >= 0; i--)
			{
				typeOperationBuilders[i].UnRegister(methodBodyBuilder, this);
			}
			RemoveModule<AsmTypeInfo>();
			RemoveModule<FieldsCollection>();
			RemoveModule<MethodsInstance>();

			var programAsset = AssetDatabase.LoadAssetAtPath<SerializedUdonProgramAsset>(programPath);
			if(programAsset == null)
			{
				programAsset = ScriptableObject.CreateInstance<SerializedUdonProgramAsset>();
				AssetDatabase.CreateAsset(programAsset, programPath);
			}
			programAsset.StoreProgram(program);
			metaCache.Sort();
			metaWriter.WriteType(classInfo.guid, metaCache);
		}

		private static void MapInterfaceMethods(Type[] interfaces, Type type, Dictionary<MethodInfo, MethodInfo> interfaceMethods)
		{
			for(var i = 0; i < interfaces.Length; i++)
			{
				var map = type.GetInterfaceMap(interfaces[i]);
				var imethods = map.InterfaceMethods;
				var dmethods = map.TargetMethods;
				for(var j = 0; j < imethods.Length; j++)
				{
					if(dmethods[j].DeclaringType != type) continue;
					if(!interfaceMethods.ContainsKey(imethods[j]))
					{
						interfaceMethods[imethods[j]] = dmethods[j];
					}
				}
			}
		}

		private struct TypeOpCodeBuider : IComparer<TypeOpCodeBuider>
		{
			public OperationBuilderDelegate Register;
			public OperationBuilderDelegate UnRegister;

			private int registerOrder;
			private Type type;

			public TypeOpCodeBuider(MethodInfo register, MethodInfo unRegister, int registerOrder, Type type)
			{
				Register = (OperationBuilderDelegate)Delegate.CreateDelegate(typeof(OperationBuilderDelegate), register);
				UnRegister = (OperationBuilderDelegate)Delegate.CreateDelegate(typeof(OperationBuilderDelegate), unRegister);
				this.registerOrder = registerOrder;
				this.type = type;
			}

			public int Compare(TypeOpCodeBuider x, TypeOpCodeBuider y)
			{
				int value = x.registerOrder.CompareTo(y.registerOrder);
				if(value != 0) return value;
				if(x.Register == y.Register) return 0;
				return x.type.AssemblyQualifiedName.CompareTo(y.type.AssemblyQualifiedName);
			}
		}
	}

	public interface IOperationBuildersRegistry
	{
		void RegisterOpBuilder(System.Reflection.Emit.OpCode code, IOperationBuider builder);
		void UnRegisterOpBuilder(System.Reflection.Emit.OpCode code, IOperationBuider builder);
	}

	/// <summary>
	/// Contains different types of modules
	/// </summary>
	public interface IModulesContainer
	{
		T GetModule<T>() where T : class;

		void AddModule<T>(T instance) where T : class;

		void RemoveModule<T>() where T : class;
	}

	public delegate void OperationBuilderDelegate(IOperationBuildersRegistry registry, IModulesContainer modules);

	public delegate void ModuleConstructorDelegate(IModulesContainer modules);

	/// <summary>
	/// Registers the type as a module, which is created once before the build.
	/// The type must contains method `Register` with <see cref="ModuleConstructorDelegate"> structure
	/// </summary>
	public sealed class StaticBuilderModuleAttribute : OrderedTypeAttributeBase
	{
		public StaticBuilderModuleAttribute(int registerOrder = 0) : base(registerOrder) { }
	}

	/// <summary>
	/// Registers a type as an opcode builder.
	/// The type must contains method `Register` with <see cref="OperationBuilderDelegate"> structure
	/// </summary>
	public sealed class OperationBuilderAttribute : OrderedTypeAttributeBase
	{
		public OperationBuilderAttribute(int registerOrder = 0) : base(registerOrder) { }
	}

	/// <summary>
	/// Registers a type as an opcode builder, but unlike the standard builder, it's created locally for each UdonProgram instance.
	/// The type must contains the `Register` and `UnRegister` methods with the <see cref="OperationBuilderDelegate"> structure.
	/// </summary>
	public sealed class TypeOperationBuilderAttribute : OrderedTypeAttributeBase
	{
		public TypeOperationBuilderAttribute(int registerOrder = 0) : base(registerOrder) { }
	}

	public interface IOperationBuider
	{
		int order { get; }

		bool Process(IMethodDescriptor method);
	}
}