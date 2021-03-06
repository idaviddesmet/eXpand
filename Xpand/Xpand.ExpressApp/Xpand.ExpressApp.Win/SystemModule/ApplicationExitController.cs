using System.ComponentModel;
using System.Windows.Forms;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Utils;
using DevExpress.ExpressApp.Win;

namespace Xpand.ExpressApp.Win.SystemModule{
    public interface IModelOptionsApplicationExit : IModelNode {
        [Category("eXpand.ApplicationExit")]
        bool MinimizeOnExit { get; set; }
        [Category("eXpand.ApplicationExit")]
        bool HideOnExit { get; set; }
        [Category("eXpand.ApplicationExit")]
        bool PromptOnExit { get; set; }
    }

    public class ApplicationExitController:WindowController, IModelExtender{
        private CloseFormController _closeFormController;

        public ApplicationExitController(){
            TargetWindowType=WindowType.Main;
        }

        protected override void OnActivated(){
            base.OnActivated();
            _closeFormController = Frame.GetController<CloseFormController>();
            _closeFormController.Close+=OnClose;
            _closeFormController.Cancel+=OnCancel;
        }

        protected override void OnDeactivated(){
            base.OnDeactivated();
            _closeFormController.Close -= OnClose;
            _closeFormController.Cancel -= OnCancel;
        }

        private void OnCancel(object sender, CancelEventArgs e){
            var options = ((IModelOptionsApplicationExit)Application.Model.Options);
            if ((options.PromptOnExit || options.MinimizeOnExit || options.HideOnExit))
                e.Cancel = true;
        }

        private void OnClose(object sender, CancelEventArgs e) {
            var modelOptionsApplicationExit = ((IModelOptionsApplicationExit)Application.Model.Options);
            var minimizeOnClose = MinimizeOnClose(modelOptionsApplicationExit);
            var hideOnClose = HideOnClose(modelOptionsApplicationExit);
            if (hideOnClose||minimizeOnClose) {
                e.Cancel = true;
            }
            else if (modelOptionsApplicationExit.PromptOnExit){
                e.Cancel = PromptOnExit();
            }
        }

        private  bool PromptOnExit() {
            var promptOnExitTitle = CaptionHelper.GetLocalizedText(XpandSystemWindowsFormsModule.XpandWin, "PromptOnExitTitle");
            var promptOnExitMessage = CaptionHelper.GetLocalizedText(XpandSystemWindowsFormsModule.XpandWin, "PromptOnExitMessage");
            return WinApplication.Messaging.GetUserChoice(promptOnExitMessage, promptOnExitTitle, MessageBoxButtons.YesNo) != DialogResult.Yes;
        }

        private bool HideOnClose(IModelOptionsApplicationExit modelOptionsApplicationExit) {
            if (modelOptionsApplicationExit.HideOnExit) {
                _closeFormController.Form.Hide();
                return true;
            }
            return false;
        }

        private bool MinimizeOnClose(IModelOptionsApplicationExit modelOptionsApplicationExit) {
            if (modelOptionsApplicationExit.MinimizeOnExit) {
                _closeFormController.Form.WindowState = FormWindowState.Minimized;
                return true;
            }
            return false;
        }

        void IModelExtender.ExtendModelInterfaces(ModelInterfaceExtenders extenders) {
            extenders.Add<IModelOptions, IModelOptionsApplicationExit>();
        }

    }
}