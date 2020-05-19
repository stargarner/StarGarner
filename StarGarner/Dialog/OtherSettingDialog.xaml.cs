using StarGarner.Util;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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
        public void lbRecord_Folder(Object sender, RoutedEventArgs e) {
            var i = lbRecord.SelectedIndex;
            if (i == -1)
                return;
            var room = uiRoomList[ i ];

            Process.Start( "EXPLORER.EXE", Path.GetFullPath( room.folder ).Replace( "/", "\\" ) );
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
                    return Task.FromResult<String?>(null);
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

        static String getRoomName(String url) {
            var m = new Regex( $"\\A{Config.URL_TOP}([^/#?]+)" ).Match( url );
            if (!m.Success) {
                throw new ArgumentException( "部屋のURLの形式が変です" );
            }
            return m.Groups[ 1 ].Value;
        }

        private void addRecord() => new OneLineTextInputDialog(
            this,
            $"録画する部屋のURL",
            "",
            (x) => {
                try {
                    var roomName = getRoomName( x );
                    foreach (var r in uiRoomList) {
                        if (r.roomName == roomName) {
                            throw new DuplicateNameException( $"部屋 {roomName} は既に登録済みです" );
                        }
                    }
                    return null;
                } catch (Exception ex) {
                    return ex.Message;
                }
            },
            async (x) => {
                try {
                    var roomName = getRoomName( x );

                    var mainWindow = this.mainWindow;
                    if (mainWindow == null)
                        throw new InvalidOperationException( "mainWindow is null." );

                    var room = await Task.Run( () => mainWindow.recorderHub.findRoom( roomName ) );
                    foreach (var r in uiRoomList) {
                        if (r.roomId == room.roomId) {
                            throw new DuplicateNameException( $"room_id {room.roomId} is duplicated. used in {r.roomName} and {room.roomName} ." );
                        }
                    }
                    var idx = uiRoomList.InsertSorted( room );
                    lbRecord.SelectedIndex = idx;
                    updateApplyButton();
                    return null;
                } catch (Exception ex) {
                    Log.e( ex, "addRecord failed." );
                    return ex.Message;
                }
            }
        ).Show();

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
            var hub = mainWindow?.recorderHub;
            if (hub == null)
                return;

            foreach (var a in uiRoomList) {
                a.isRecordingUi = hub.findRecording( a.roomName )?.isRunning == true;
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

            updateApplyButton();
            showServerStatus();

            showRecorderStatus();
        }
    }
}
