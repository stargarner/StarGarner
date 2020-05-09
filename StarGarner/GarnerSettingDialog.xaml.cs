using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace StarGarner {

    internal static class RemoveMinMaxButton {
        private const Int32 GWL_STYLE = -16,
                       WS_MAXIMIZEBOX = 0x10000,
                       WS_MINIMIZEBOX = 0x20000;

        [DllImport( "user32.dll" )]
        extern private static Int32 GetWindowLong(IntPtr hwnd, Int32 index);

        [DllImport( "user32.dll" )]
        extern private static Int32 SetWindowLong(IntPtr hwnd, Int32 index, Int32 value);

        internal static void HideMinimizeAndMaximizeButtons(this Window window) {
            var hwnd = new System.Windows.Interop.WindowInteropHelper( window ).Handle;
            var currentStyle = GetWindowLong( hwnd, GWL_STYLE );

            SetWindowLong( hwnd, GWL_STYLE, currentStyle & ~WS_MAXIMIZEBOX & ~WS_MINIMIZEBOX );
        }
    }

    public class SoundType {
        public String Name {
            get; set;
        } = "?";
    }

    public partial class GarnerSettingDialog : Window {

        private readonly Garner garner;
        private readonly NotificationSound notificationSound = new NotificationSound();

        private Boolean isChanged() {
            var changed = false;

            var st = (SoundType?)lbSoundActor.SelectedItem;
            if (st != null && st.Name != garner.soundActor) changed = true;

            return changed;
        }

        void save() {
            if (!isChanged())
                return;

            var st = (SoundType?)lbSoundActor.SelectedItem;
            if (st != null) garner.soundActor = st.Name;

            var mainWindow = (MainWindow?)Owner;
            mainWindow?.saveGarnerSetting( garner );
        }

        private void updateApplyButton() => btnApply.IsEnabled = isChanged();


        public Boolean isClosed = false;

        protected override void OnClosed(EventArgs e) {
            isClosed = true;
            base.OnClosed( e );
            notificationSound.Dispose();
        }

        public GarnerSettingDialog(Garner garner) {
            InitializeComponent();
            this.HideMinimizeAndMaximizeButtons();
            this.SourceInitialized += (x, y) => this.HideMinimizeAndMaximizeButtons();

            this.garner = garner;

            Title = $"{garner.itemName}の設定";

            btnCancel.Click += (sender, e) => Close();
            btnOk.Click += (sender, e) => { save(); Close(); };
            btnApply.Click += (sender, e) => {
                save();
                updateApplyButton();
            };

            var soundList = new ObservableCollection<SoundType>();
            var source = NotificationSound.actors;
            Int32? selectedIndex = null;
            for (Int32 i = 0, ie = source.Count; i < ie; ++i) {
                var name = source[ i ];
                soundList.Add( new SoundType() { Name = name } );
                if (name == garner.soundActor) {
                    selectedIndex = i;
                }
            }
            lbSoundActor.ItemsSource = soundList;
            lbSoundActor.SelectedIndex = selectedIndex ?? 0;
            lbSoundActor.SelectionChanged += (sender, e) => updateApplyButton();

            btnTestSoundActor.Click += (sender, e) => {
                var st = (SoundType?)lbSoundActor.SelectedItem;
                if (st != null)
                    notificationSound.play( st.Name, NotificationSound.liveStart );
            };

            updateApplyButton();
        }

        
    }
}
