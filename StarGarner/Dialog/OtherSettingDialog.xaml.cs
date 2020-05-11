using StarGarner.Util;
using System;
using System.Windows;

namespace StarGarner.Dialog {

    public partial class OtherSettingDialog : Window {

        public Boolean isClosed = false;

        private MainWindow? mainWindow => (MainWindow?)Owner;

        private Boolean isChanged() {
            var changed = false;

            var responseLog = cbResponseLog.IsChecked ?? false;
            if (MyResourceRequestHandler.responseLogEnabled != responseLog) {
                changed = true;
            }

            var mainWindow = this.mainWindow;
            if (mainWindow != null) {

                var listenEnabled = cbListen.IsChecked ?? false;
                if (mainWindow.httpServer.enabled != listenEnabled) {
                    changed = true;
                }

                var listenAddr = tbListenAddress.Text.Trim();
                if (mainWindow.httpServer.listenAddr != listenAddr) {
                    changed = true;
                }

                var listenPort = tbListenPort.Text.Trim();
                if (mainWindow.httpServer.listenPort != listenPort) {
                    changed = true;
                }
            }


            return changed;
        }

        void save() {
            if (!isChanged())
                return;

            var mainWindow = this.mainWindow;
            if (mainWindow == null)
                return;

            var responseLog = cbResponseLog.IsChecked ?? false;
            MyResourceRequestHandler.responseLogEnabled = responseLog;

            mainWindow.httpServer.enabled = cbListen.IsChecked ?? false;
            mainWindow.httpServer.listenAddr = tbListenAddress.Text.Trim();
            mainWindow.httpServer.listenPort = tbListenPort.Text.Trim();
            mainWindow.httpServer.updateListening();

            mainWindow.saveOtherSetting();
        }

        internal void showServerStatus() => Dispatcher.BeginInvoke( (Action)( () => {
            var sv = mainWindow?.httpServer.serverError ?? "";
            tbListenError.Visibility = sv.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            tbListenError.Text = sv;
        } ) );


        private void updateApplyButton() => btnApply.IsEnabled = isChanged();

        protected override void OnClosed(EventArgs e) {
            isClosed = true;
            base.OnClosed( e );
        }

        public OtherSettingDialog(MainWindow main) {
            InitializeComponent();
            this.SourceInitialized += (x, y) => this.HideMinimizeAndMaximizeButtons();

            this.Owner = main;

            // load ui value
            cbResponseLog.IsChecked = MyResourceRequestHandler.responseLogEnabled;
            cbListen.IsChecked = main.httpServer?.enabled ?? false;
            tbListenAddress.Text = main.httpServer?.listenAddr ?? "";
            tbListenPort.Text = main.httpServer?.listenPort ?? "";

            // add event handler
            cbResponseLog.Checked += (sender, e) => updateApplyButton();
            cbResponseLog.Unchecked += (sender, e) => updateApplyButton();
            cbListen.Checked += (sender, e) => updateApplyButton();
            cbListen.Unchecked += (sender, e) => updateApplyButton();
            tbListenAddress.TextChanged += (sender, e) => updateApplyButton();
            tbListenPort.TextChanged += (sender, e) => updateApplyButton();

            // add event handler to bottom buttons
            btnCancel.Click += (sender, e) => Close();
            btnOk.Click += (sender, e) => { save(); Close(); };
            btnApply.Click += (sender, e) => { save(); updateApplyButton(); };

            updateApplyButton();
            showServerStatus();

        }
    }
}
