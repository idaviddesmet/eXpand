﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Model.Core;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.Persistent.Base;
using Xpand.Persistent.Base.General.Model;
using Xpand.Utils.Linq;

namespace Xpand.Persistent.Base.General.Controllers {
    [ModelAbstractClass]
    public interface IModelNavigationItemDataSource:IModelNavigationItem{
        [Category(AttributeCategoryNameProvider.Xpand)]
        [DataSourceProperty("Application.ListViews")]
        IModelListView DatasourceListView { get; set; }
    }
    public class NavigationItemsController:WindowController,IModelExtender{
        private bool _recreate;

        protected override void OnFrameAssigned(){
            base.OnFrameAssigned();
            if (Frame.Context==TemplateContext.ApplicationWindow){
                Application.ObjectSpaceCreated += ApplicationOnObjectSpaceCreated;
                Frame.GetController<ShowNavigationItemController>(showNavigationItemController => {
                    showNavigationItemController.CustomInitializeItems += OnCustomInitializeItems;
                    showNavigationItemController.ItemsInitialized += ShowNavigationItemControllerOnItemsInitialized;
                });
                Frame.Disposing+=FrameOnDisposing;
            }
        }

        private void FrameOnDisposing(object sender, EventArgs eventArgs){
            Frame.Disposing -= FrameOnDisposing;
            Application.ObjectSpaceCreated -= ApplicationOnObjectSpaceCreated;
            Frame.GetController<ShowNavigationItemController>(showNavigationItemController => {
                showNavigationItemController.CustomInitializeItems -= OnCustomInitializeItems;
                showNavigationItemController.ItemsInitialized -= ShowNavigationItemControllerOnItemsInitialized;
            } );
        }


        private void ApplicationOnObjectSpaceCreated(object sender, ObjectSpaceCreatedEventArgs e){
            var objectSpace = e.ObjectSpace;
            objectSpace.Committed+=ObjectSpaceOnCommitted;
            objectSpace.Committing+=ObjectSpaceOnCommitting;
            objectSpace.Disposed+=ObjectSpaceOnDisposed;
        }

        private void ObjectSpaceOnDisposed(object sender, EventArgs eventArgs){
            var objectSpace = ((IObjectSpace) sender);
            objectSpace.Committed-=ObjectSpaceOnCommitted;
            objectSpace.Committing-=ObjectSpaceOnCommitting;
            objectSpace.Disposed-=ObjectSpaceOnDisposed;
        }

        private void ObjectSpaceOnCommitting(object sender, CancelEventArgs cancelEventArgs){
            var types = NavigationItemDataSources.Select(source => source.DatasourceListView.ModelClass.TypeInfo.Type);
            _recreate = !cancelEventArgs.Cancel &&
                        ((IObjectSpace) sender).ModifiedObjects.Cast<object>().Any(o => types.Contains(o.GetType()));

        }

        private void ObjectSpaceOnCommitted(object sender, EventArgs eventArgs){
            if (_recreate) {
                Frame.Application.MainWindow.GetController<ShowNavigationItemController>(controller => controller.RecreateNavigationItems());
                _recreate = false;
            }
        }

        private void ShowNavigationItemControllerOnItemsInitialized(object sender, EventArgs eventArgs){
            ((ModelApplicationBase) Application.Model).RemoveLayer();
        }

        private void OnCustomInitializeItems(object sender, HandledEventArgs handledEventArgs){
            handledEventArgs.Handled = false;
            var modelApplicationBase = ((ModelApplicationBase) Application.Model);
            var modelApplication = modelApplicationBase.CreatorInstance.CreateModelApplication();
            modelApplicationBase.AddLayer(modelApplication);

            var navigationItems = NavigationItemDataSources;

            modelApplication.Id = GetType().FullName;
            foreach (var navigationItemGroup in navigationItems.GroupBy(source => source.DatasourceListView)){
                using (var objectSpace = Application.CreateObjectSpace(navigationItemGroup.Key.ModelClass.TypeInfo.Type)){
                    foreach (var navigationItem in navigationItemGroup){
                        navigationItem.View = null;
                        var datasourceListView = navigationItem.DatasourceListView;
                        var typeInfo = datasourceListView.ModelClass.TypeInfo;
                        var collectionSourceBase = Application.CreateCollectionSource(objectSpace, typeInfo.Type,
                            datasourceListView.Id);
                        Application.CreateListView(datasourceListView, collectionSourceBase, true);
                        var proxyCollection = (ProxyCollection)collectionSourceBase.Collection;
                        for (var index = 0; index < proxyCollection.Count; index++) {
                            var obj = proxyCollection[index];
                            var caption = datasourceListView.Columns.First().ModelMember.MemberInfo.GetValue(obj) + "";
                            var id = datasourceListView.ModelClass.TypeInfo.KeyMember.GetValue(obj) + "";
                            CreateChildNavigationItem(navigationItem, index, caption, id);
                        }
                    }
                }
            }
            
        }

        private IEnumerable<IModelNavigationItemDataSource> NavigationItemDataSources{
            get{
                return ((IModelApplicationNavigationItems) Application.Model).NavigationItems.Items
                        .GetItems<IModelNavigationItemDataSource>(source => source.Items).Where(source => source.DatasourceListView != null);
            }
        }

        private void CreateChildNavigationItem(IModelNavigationItemDataSource navigationItem, int index, string caption,string id){
            if (navigationItem.Items[id]!=null)
                return;
            var modelNavigationItem = navigationItem.Items.AddNode<IModelNavigationItem>(id);
            modelNavigationItem.View=navigationItem.DatasourceListView.DetailView;
            modelNavigationItem.Index = index;
            modelNavigationItem.Caption = caption;
            modelNavigationItem.ObjectKey = @"" + modelNavigationItem.Id;
            modelNavigationItem.ImageName = navigationItem.DatasourceListView.ImageName;
        }

        public void ExtendModelInterfaces(ModelInterfaceExtenders extenders){
            extenders.Add<IModelNavigationItem, IModelNavigationItemDataSource>();
        }
    }
}
