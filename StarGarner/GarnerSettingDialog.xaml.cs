using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace StarGarner {

    public class ListItemActor {
        public String Name {
            get; set;
        } = "?";
    }

    public partial class GarnerSettingDialog : Window {

        static readonly Random random = new Random();

        private readonly Garner garner;

        private MainWindow? mainWindow => (MainWindow?)Owner;

        private Boolean isChanged() {
            var changed = false;

            var st = (ListItemActor?)lbSoundActor.SelectedItem;
            if (st != null && st.Name != garner.soundActor)
                changed = true;

            return changed;
        }

        void save() {
            if (!isChanged())
                return;

            var st = (ListItemActor?)lbSoundActor.SelectedItem;
            if (st != null)
                garner.soundActor = st.Name;

            mainWindow?.saveGarnerSetting( garner );
        }

        private void updateApplyButton() => btnApply.IsEnabled = isChanged();

        void testSound() {
            var st = (ListItemActor?)lbSoundActor.SelectedItem;
            if (st == null)
                return;

            var soundName = NotificationSound.all[ random.Next( NotificationSound.all.Count ) ];

            mainWindow?.notificationSound?.play( st.Name, soundName );
        }

        //############################################################

        public Boolean isClosed = false;

        protected override void OnClosed(EventArgs e) {
            isClosed = true;
            base.OnClosed( e );
        }

        public GarnerSettingDialog(Garner garner) {
            InitializeComponent();
            this.SourceInitialized += (x, y) => this.HideMinimizeAndMaximizeButtons();

            this.garner = garner;

            Title = $"{garner.itemName}の設定";

            btnCancel.Click += (sender, e) => Close();
            btnOk.Click += (sender, e) => { save(); Close(); };
            btnApply.Click += (sender, e) => {
                save();
                updateApplyButton();
            };

            var soundList = new ObservableCollection<ListItemActor>();
            var source = NotificationSound.actors;
            Int32? selectedIndex = null;
            for (Int32 i = 0, ie = source.Count; i < ie; ++i) {
                var name = source[ i ];
                soundList.Add( new ListItemActor() { Name = name } );
                if (name == garner.soundActor) {
                    selectedIndex = i;
                }
            }
            lbSoundActor.ItemsSource = soundList;
            lbSoundActor.SelectedIndex = selectedIndex ?? 0;
            lbSoundActor.SelectionChanged += (sender, e) => updateApplyButton();

            btnTestSoundActor.Click += (sender, e) => testSound();

            updateApplyButton();
        }
    }
}
