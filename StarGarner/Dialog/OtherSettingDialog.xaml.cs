using StarGarner.Util;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
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

                var saveDir = tbRecordSaveDir.Text.Trim();
                if (mainWindow.recorderHub.saveDir != saveDir) {
                    changed = true;
                }

                var ffmpegPath = tbRecordFfmpegPath.Text.Trim();
                if (mainWindow.recorderHub.ffmpegPath != ffmpegPath) {
                    changed = true;
                }

                if (!mainWindow.recorderHub.equalsRoomList( uiRoomList )) {
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

            mainWindow.recorderHub.saveDir = tbRecordSaveDir.Text.Trim();
            mainWindow.recorderHub.ffmpegPath = tbRecordFfmpegPath.Text.Trim();
            mainWindow.recorderHub.setRoomList( uiRoomList );

            mainWindow.saveOtherSetting();
        }

        internal void showServerStatus() => Dispatcher.BeginInvoke( (Action)( () => {
            if (isClosed)
                return;
            var sv = mainWindow?.httpServer.serverStatus ?? "";
            tbListenError.textOrGone( sv );
        } ) );


        private void updateApplyButton() => btnApply.IsEnabled = isChanged();

        //############################################################################

        readonly ObservableCollection<RecordRoom> uiRoomList = new ObservableCollection<RecordRoom>();

        public void lbRecord_View(Object sender, RoutedEventArgs e) {
            var i = lbRecord.SelectedIndex;
            if (i == -1)
                return;
            var room = uiRoomList[ i ];
           
            var p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "cmd";
            p.StartInfo.Arguments = $"/C start {room.url}";
            p.Start();
        }

        public void lbRecord_Edit(Object sender, RoutedEventArgs e) {
            var i = lbRecord.SelectedIndex;
            if (i == -1)
                return;
            var room = uiRoomList[ i ];
            new OneLineTextInputDialog(
                this,
                $"{room.roomName}の説明文",
                room.roomCaption,
                (x) => null,
                (x) => {
                    room.roomCaption = x;
                    lbRecord.Items.Refresh();
                    updateApplyButton();
                }
                ).Show();
        }
        public void lbRecord_Delete(Object sender, RoutedEventArgs e) {
            var i = lbRecord.SelectedIndex;
            if (i == -1)
                return;
            uiRoomList.RemoveAt( i );
            updateApplyButton();
        }

        private void addRecord() {
            var text = tbRoomUrl.Text.Trim();

            var mainWindow = this.mainWindow;
            if (mainWindow == null)
                return;

            Task.Run( async () => {
                try {
                    var room = await mainWindow.recorderHub.findRoom( text );

                    await Dispatcher.RunAsync( () => {
                        foreach (var ui in uiRoomList) {
                            if (ui.roomName == room.roomName) {
                                throw new DuplicateNameException( $"{room.roomName}は既に含まれています" );
                            } else if (ui.roomId == room.roomId) {
                                throw new DuplicateNameException( $"room_id {room.roomId} is duplicated. used in {ui.roomName} and {room.roomName} ." );
                            }
                        }
                        var idx = uiRoomList.InsertSorted( room );
                        lbRecord.SelectedIndex = idx;
                        tbRoomUrl.Text = "";
                        updateApplyButton();
                    } );
                } catch (DuplicateNameException ex) {
                    MessageBox.Show( ex.Message );
                } catch (Exception ex) {
                    MessageBox.Show( ex.ToString() );
                }
            } );
        }

        private void loadRecorderItems() {
            var source = mainWindow?.recorderHub?.getList();
            if (source == null)
                return;
            source.Sort();

            uiRoomList.Clear();
            foreach (var room in source) {
                uiRoomList.Add( room.clone() );
            }
            lbRecord.ItemsSource = uiRoomList;
        }

        internal void showRecorderStatus() {
            var source = mainWindow?.recorderHub?.getList();
            if (source != null) {
                foreach (var a in uiRoomList) {
                    var r = source.Find( (x) => x.roomName == a.roomName );
                    a.isRecordingUi = r == null ? false : r.recording?.isRunning == true;
                }
            }
            lbRecord.Items.Refresh();
        }

        //############################################################################

        protected override void OnClosed(EventArgs e) {
            isClosed = true;
            base.OnClosed( e );
        }

        public OtherSettingDialog(MainWindow main) {
            this.Owner = main;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.SourceInitialized += (x, y) => this.HideMinimizeAndMaximizeButtons();

            InitializeComponent();

            // load ui value
            cbResponseLog.IsChecked = MyResourceRequestHandler.responseLogEnabled;
            cbListen.IsChecked = main.httpServer.enabled;
            tbListenAddress.Text = main.httpServer.listenAddr;
            tbListenPort.Text = main.httpServer.listenPort;
            tbRecordSaveDir.Text = main.recorderHub.saveDir;
            tbRecordFfmpegPath.Text = main.recorderHub.ffmpegPath;
            loadRecorderItems();

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

            btnRecordAdd.Click += (sender, e) => addRecord();
            lbRecord.SelectionChanged += (sender, e) => {
                var i = lbRecord.SelectedIndex;
                if (i != -1) {
                    tbRoomUrl.Text = uiRoomList[ i ].url;
                }
            };

            updateApplyButton();
            showServerStatus();

            showRecorderStatus();
        }
    }
}
