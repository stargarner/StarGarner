using StarGarner.Model;
using StarGarner.Util;
using System;
using System.Windows;

namespace StarGarner.Dialog {

    public partial class GarnerSettingDialog : Window {

        private readonly Garner garner;

        private readonly MainWindow mainWindow;

        public Boolean isClosed = false;

        private SoundActor? selectedSoundActor => (SoundActor?)lbSoundActor.SelectedItem;

        void testSound()
            => selectedSoundActor?.test( mainWindow.notificationSound, NotificationSound.allGarner );

        //############################################################

        private Boolean isChanged() => Utils.or(
            garner.soundActor != selectedSoundActor?.Name
            );

        private void updateApplyButton() => btnApply.IsEnabled = isChanged();

        void save() {
            if (!isChanged())
                return;

            var st = selectedSoundActor;
            if (st != null)
                garner.soundActor = st.Name;

            mainWindow.saveGarnerSetting( garner );
        }

        //############################################################

        protected override void OnClosed(EventArgs e) {
            isClosed = true;
            base.OnClosed( e );
        }

        public GarnerSettingDialog(MainWindow mainWindow, Garner garner) {
            this.mainWindow = mainWindow;
            this.garner = garner;
            this.Owner = mainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.SourceInitialized += (x, y) => this.HideMinimizeAndMaximizeButtons();

            InitializeComponent();

            Title = $"{garner.itemName}の設定";

            SoundActor.initListBox( lbSoundActor, garner.soundActor );
            lbSoundActor.SelectionChanged += (sender, e) => updateApplyButton();
            btnTestSoundActor.Click += (sender, e) => testSound();

            btnCancel.Click += (sender, e) => Close();
            btnOk.Click += (sender, e) => { save(); Close(); };
            btnApply.Click += (sender, e) => { save(); updateApplyButton(); };

            updateApplyButton();
        }
    }
}
