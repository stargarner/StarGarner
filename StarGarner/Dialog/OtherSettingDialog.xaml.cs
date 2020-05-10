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

            return changed;
        }

        void save() {
            if (!isChanged())
                return;

            var responseLog = cbResponseLog.IsChecked ?? false;
            MyResourceRequestHandler.responseLogEnabled = responseLog;

            mainWindow?.saveOtherSetting();
        }

        private void updateApplyButton() => btnApply.IsEnabled = isChanged();

        protected override void OnClosed(EventArgs e) {
            isClosed = true;
            base.OnClosed( e );
        }

        public OtherSettingDialog() {
            InitializeComponent();
            this.SourceInitialized += (x, y) => this.HideMinimizeAndMaximizeButtons();

            cbResponseLog.IsChecked = MyResourceRequestHandler.responseLogEnabled;

            cbResponseLog.Checked += (sender, e) => updateApplyButton();
            cbResponseLog.Unchecked += (sender, e) => updateApplyButton();

            btnCancel.Click += (sender, e) => Close();
            btnOk.Click += (sender, e) => { save(); Close(); };
            btnApply.Click += (sender, e) => { save(); updateApplyButton(); };

            updateApplyButton();
        }
    }
}
