using System;
using System.ComponentModel;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model.Core;
using DevExpress.ExpressApp.Security.ClientServer;
using DevExpress.ExpressApp.Validation;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Validation;
using DevExpress.Utils;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using Fasterflect;
using Xpand.ExpressApp.Validation;
using Xpand.ExpressApp.WorldCreator.BusinessObjects.Validation;
using Xpand.ExpressApp.WorldCreator.CodeProvider;
using Xpand.ExpressApp.WorldCreator.CodeProvider.Validation;
using Xpand.ExpressApp.WorldCreator.Services;
using Xpand.ExpressApp.WorldCreator.System;
using Xpand.ExpressApp.WorldCreator.System.NodeUpdaters;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.ModelDifference;
using Xpand.Utils.Helpers;
using EditorAliases = Xpand.Persistent.Base.General.EditorAliases;

namespace Xpand.ExpressApp.WorldCreator {

    [ToolboxItem(true)]
    [ToolboxTabName(XpandAssemblyInfo.TabWinWebModules)]
    public sealed class WorldCreatorModule : XpandModuleBase, IAdditionalModuleProvider {
        public const string BaseImplNameSpace = "Xpand.Persistent.BaseImpl.PersistentMetaData";

        public WorldCreatorModule() {
            RequiredModuleTypes.Add(typeof(XpandValidationModule));
            RequiredModuleTypes.Add(typeof(ConditionalAppearanceModule));
        }
        private readonly object _locker = new object();
        public const string WCAssembliesPath = "WCAssembliesPath";

        public ModuleBase[] DynamicModules => ModuleManager.Modules.Where(
            module => module.GetType().Assembly.ManifestModule.ScopeName.EndsWith(Compiler.XpandExtension))
            .ToArray();

        public override void AddGeneratorUpdaters(ModelNodesGeneratorUpdaters updaters) {
            base.AddGeneratorUpdaters(updaters);
            updaters.Add(new ImageSourcesUpdater(DynamicModules));
        }


        private void ApplicationOnLoggedOn(object sender, LogonEventArgs logonEventArgs) {
            AddDynamicModulesObjectSpaceProviders();
        }

        private void AddDynamicModulesObjectSpaceProviders() {
            var providerBuilder = new DatastoreObjectSpaceProviderBuilder(DynamicModules);
            providerBuilder.CreateProviders().Each(provider => Application.AddObjectSpaceProvider(provider));
        }

        void AddPersistentModules(ApplicationModulesManager applicationModulesManager) {

            CompatibilityCheckerApplication.CheckCompatibility(Application);

            if (!string.IsNullOrEmpty(ConnectionString)) {
                lock (_locker) {
                    var worldCreatorObjectSpaceProvider = WorldCreatorObjectSpaceProvider.Create(Application, false);
                    using (var objectSpace = worldCreatorObjectSpaceProvider.CreateObjectSpace()) {
                        var codeValidator = new CodeValidator(new Compiler(AssemblyPathProvider.Instance.GetPath(Application)), new AssemblyValidator());
                        var assemblyManager = new AssemblyManager(objectSpace, codeValidator);
                        foreach (var assembly in assemblyManager.LoadAssemblies()) {
                            var moduleType = assembly.GetTypes().First(type => typeof(ModuleBase).IsAssignableFrom(type));
                            applicationModulesManager.AddModule(Application, (ModuleBase)moduleType.CreateInstance());
                        }
                        worldCreatorObjectSpaceProvider.ResetThreadSafe();
                    }
                }
            }
            else {
                var assemblies =
                    AppDomain.CurrentDomain.GetAssemblies()
                        .Where(assembly => assembly.ManifestModule.ScopeName.EndsWith(Compiler.XpandExtension));
                foreach (var assembly1 in assemblies) {
                    applicationModulesManager.AddModule(assembly1.GetTypes().First(type => typeof(ModuleBase).IsAssignableFrom(type)));
                }
            }
            XpoObjectMerger.MergeTypes(this);
        }

        public override void CustomizeTypesInfo(ITypesInfo typesInfo) {
            base.CustomizeTypesInfo(typesInfo);
            CheckIfSupported();
            AddToAdditionalExportedTypes(BaseImplNameSpace);
            var classInfos = XpoTypesInfoHelper.GetXpoTypeInfoSource().XPDictionary.Classes.OfType<XPClassInfo>().Where(info => info.IsPersistent &&
                            WorldCreatorTypeInfoSource.Instance.RegisteredEntities.Contains(info.ClassType));
            foreach (var xpClassInfo in classInfos) {
                xpClassInfo.AddAttribute(new NonPersistentAttribute());
            }
            ExistentTypesMemberCreator.CreateMembers(this);
        }

        public override void Setup(ApplicationModulesManager moduleManager) {
            base.Setup(moduleManager);
            CheckIfSupported();
            AddToAdditionalExportedTypes(BaseImplNameSpace);
            ValidationRulesRegistrator.RegisterRule(moduleManager, typeof(RuleClassInfoMerge), typeof(IRuleBaseProperties));
            ValidationRulesRegistrator.RegisterRule(moduleManager, typeof(RuleValidCodeIdentifier), typeof(IRuleBaseProperties));
            if (Application != null && (RuntimeMode || !string.IsNullOrEmpty(ConnectionString))) {
                AddPersistentModules(moduleManager);
                RegisterDerivedTypes();
                Application.LoggedOn += ApplicationOnLoggedOn;
            }
        }

        void IAdditionalModuleProvider.AddAdditionalModules(ApplicationModulesManager applicationModulesManager) {
            AddToAdditionalExportedTypes(BaseImplNameSpace);
            AddPersistentModules(applicationModulesManager);
        }

        private void CheckIfSupported() {
            var dataServerObjectSpaceProvider = Application?.ObjectSpaceProvider as DataServerObjectSpaceProvider;
            if (dataServerObjectSpaceProvider != null)
                throw new NotSupportedException(Application.ObjectSpaceProvider.GetType().FullName);
        }

        private void RegisterDerivedTypes() {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => new[] { "System", "DevExpress" }.All(s => !assembly.GetName().Name.StartsWith(s)));
            var types = assemblies.SelectMany(assembly => assembly.GetTypes());
            var additionalTypes = AdditionalExportedTypes.Where(type => type.Namespace != null && type.Namespace.StartsWith(BaseImplNameSpace)).ToArray();
            types = types.Where(type => type.Assembly != BaseImplAssembly && additionalTypes.Any(type1 => type1.IsAssignableFrom(type)));
            foreach (var type in types) {
                WorldCreatorTypeInfoSource.Instance.ForceRegisterEntity(type);
            }
        }

        protected override void RegisterEditorDescriptors(EditorDescriptorsFactory editorDescriptorsFactory) {
            base.RegisterEditorDescriptors(editorDescriptorsFactory);
            editorDescriptorsFactory.List.Add(new PropertyEditorDescriptor(new AliasRegistration(EditorAliases.CSCodePropertyEditor, typeof(string), false)));
        }
    }
}

