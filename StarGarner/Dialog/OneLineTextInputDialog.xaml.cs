using StarGarner.Util;
using System;
using System.Windows;

namespace StarGarner.Dialog {
    /// <summary>
    /// OneLineTextInputDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class OneLineTextInputDialog : Window {
        private readonly String initialValue;
        private readonly Func<String, String?> validator;

        private void updateOkButton() {
            var sv = tbContent.Text.ToString();
            var error = validator( sv );
            tbError.textOrGone( error ?? "" );
            btnOk.IsEnabled = error == null && initialValue != sv;
        }

        public OneLineTextInputDialog(Window parent, String caption, String initialValue, Func<String, String?> validator, Action<String> onOk) {
            this.Owner = parent;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.validator = validator;

            InitializeComponent();

            lbCaption.Content = caption;
            tbContent.Text = initialValue;
            this.initialValue = initialValue;

            tbContent.TextChanged += (sender, e) => updateOkButton();

            btnCancel.Click += (sender, e) => Close();
            btnOk.Click += (sender, e) => { onOk( tbContent.Text.ToString().Trim() ); Close(); };
            updateOkButton();
        }
    }
}
