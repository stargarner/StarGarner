using StarGarner.Util;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace StarGarner.Dialog {
    public partial class OneLineTextInputDialog : Window {

        public enum InputStyle {
            None,
            RoomUrl
        }

        private readonly String initialValue;
        private readonly Func<String, String?> validator;

        private Boolean updateOkButton() {
            var sv = tbContent.Text.ToString();
            var error = validator( sv );
            tbError.textOrGone( error ?? "" );
            var enabled = error == null && initialValue != sv;
            btnOk.IsEnabled = enabled;
            return enabled;
        }

        public OneLineTextInputDialog(
            Window parent, String caption, String initialValue,
            Func<String, String?> validator,
            Func<String, Task<String?>> onOk,
            InputStyle inputRestriction = InputStyle.None
            ) {
            this.Owner = parent;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.SourceInitialized += (x, y) => this.HideMinimizeAndMaximizeButtons();

            this.initialValue = initialValue;
            this.validator = validator;

            InitializeComponent();

            switch (inputRestriction) {
            default:
                break;
            case InputStyle.RoomUrl:

                // タッチキーボードの文字種を変更する
                var scope = new InputScope();
                scope.Names.Add( new InputScopeName { NameValue = InputScopeNameValue.Url } );
                tbContent.InputScope = scope;

                break;
            }

            lbCaption.Content = caption;
            tbContent.Text = initialValue;

            tbContent.Focus();

            updateOkButton();

            tbContent.TextChanged += (sender, e) => updateOkButton();
            btnCancel.Click += (sender, e) => Close();
            tbContent.KeyDown += (sender, e) => {
                if (e.Key == Key.Enter) {
                    btnOk.RaiseEvent( new RoutedEventArgs( ButtonBase.ClickEvent ) );
                }
            };

            btnOk.Click += async (sender, e) => {
                if (!updateOkButton())
                    return;

                var text = tbContent.Text.ToString().Trim();
                var error = await onOk( text );
                if (error != null) {
                    tbError.textOrGone( error ?? "" );
                    btnOk.IsEnabled = false;
                } else {
                    Close();
                }
            };
        }
    }
}
