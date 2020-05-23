using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace StarGarner.Model {
    public class SoundActor {

        static readonly Random random = new Random();

        public String Name {
            get; set;
        } = "?";

        internal void test(NotificationSound? notificationSound,List<String> list)
            => notificationSound?.play(Name, list[ random.Next( list.Count ) ] );

        internal static void initListBox(ListBox lbSoundActor, String? initialSoundActor) {
            var soundList = new ObservableCollection<SoundActor>();
            var source = NotificationSound.actors;
            var selectedIndex = 0;
            for (Int32 i = 0, ie = source.Count; i < ie; ++i) {
                var name = source[ i ];
                soundList.Add( new SoundActor() { Name = name } );
                if (name == initialSoundActor) {
                    selectedIndex = i;
                }
            }
            lbSoundActor.ItemsSource = soundList;
            lbSoundActor.SelectedIndex = selectedIndex;
        }
    }
}
