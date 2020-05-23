using StarGarner.Model;
using StarGarner.Util;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace StarGarner.Dialog {



    public partial class GarnerSettingDialog : Window {



        private readonly Garner garner;

        private MainWindow? mainWindow => (MainWindow?)Owner;

        private Boolean isChanged() {
            var changed = false;

            var st = (SoundActor?)lbSoundActor.SelectedItem;
            if (st != null && st.Name != garner.soundActor)
                changed = true;

            return changed;
        }

        void save() {
            if (!isChanged())
                return;

            var st = (SoundActor?)lbSoundActor.SelectedItem;
            if (st != null)
                garner.soundActor = st.Name;

            mainWindow?.saveGarnerSetting( garner );
        }

        private void updateApplyButton() => btnApply.IsEnabled = isChanged();

        void testSound()
            => ( (SoundActor?)lbSoundActor.SelectedItem )?.test( mainWindow?.notificationSound, NotificationSound.allGarner );

        //############################################################

        public Boolean isClosed = false;

        protected override void OnClosed(EventArgs e) {
            isClosed = true;
            base.OnClosed( e );
        }

        public GarnerSettingDialog(Window parent, Garner garner) {
            this.Owner = parent;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.SourceInitialized += (x, y) => this.HideMinimizeAndMaximizeButtons();

            InitializeComponent();

            this.garner = garner;

            Title = $"{garner.itemName}の設定";

            btnCancel.Click += (sender, e) => Close();
            btnOk.Click += (sender, e) => { save(); Close(); };
            btnApply.Click += (sender, e) => {
                save();
                updateApplyButton();
            };

            SoundActor.initListBox( lbSoundActor, garner.soundActor );
            lbSoundActor.SelectionChanged += (sender, e) => updateApplyButton();
            btnTestSoundActor.Click += (sender, e) => testSound();

            updateApplyButton();
        }
    }
}
