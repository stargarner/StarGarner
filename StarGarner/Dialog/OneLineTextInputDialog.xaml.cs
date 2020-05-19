using StarGarner.Util;
using System;
using System.Threading.Tasks;
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

        public OneLineTextInputDialog(
            Window parent, String caption, String initialValue,
            Func<String, String?> validator, Func<String, Task<String?>> onOk) {
            this.Owner = parent;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.SourceInitialized += (x, y) => this.HideMinimizeAndMaximizeButtons();

            this.initialValue = initialValue;
            this.validator = validator;

            InitializeComponent();

            lbCaption.Content = caption;
            tbContent.Text = initialValue;
            tbContent.Focus();

            tbContent.TextChanged += (sender, e) => updateOkButton();
            btnCancel.Click += (sender, e) => Close();

            btnOk.Click += async (sender, e) => {
                var text = tbContent.Text.ToString().Trim();
                var error = await onOk( text );
                if (error != null) {
                    tbError.textOrGone( error ?? "" );
                    btnOk.IsEnabled = false;
                } else {
                    Close();
                }
            };

            updateOkButton();
        }
    }
}
