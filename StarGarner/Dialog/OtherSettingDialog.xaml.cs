using StarGarner.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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

                if (mainWindow.recorderHub.isRoomListChanged( uiRoomList )) {
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

            Process.Start( "EXPLORER.EXE", Path.GetFullPath( room.getFolder( mainWindow!.recorderHub ) ).Replace( "/", "\\" ) );
        }

        public void lbRecord_Edit(Object sender, RoutedEventArgs e) {
            var i = lbRecord.SelectedIndex;
            if (i == -1)
                return;
            var room = uiRoomList[ i ];
            new OneLineTextInputDialog(
                this,
                caption: $"{room.roomName}の説明文",
                initialValue: room.roomCaption,
                validator: (x) => null,
                onOk: (x) => {
                    room.roomCaption = x;
                    lbRecord.Items.Refresh();
                    updateApplyButton();
                    return Task.FromResult<String?>( null );
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

        static String getRoomName(String url)
            => new Regex( $"\\A{Config.URL_TOP}([^/#?]+)" ).matchOrNull( url )
                ?.Groups[ 1 ].Value
                ?? throw new ArgumentException( "部屋のURLの形式が変です" );

        private void addRecord() => new OneLineTextInputDialog(
            this,
            caption: $"録画する部屋のURL",
            initialValue: "",
            inputRestriction: OneLineTextInputDialog.InputStyle.RoomUrl,
            validator: (x) => {
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
            onOk: async (x) => {
                try {
                    var roomName = getRoomName( x );

                    var mainWindow = this.mainWindow;
                    if (mainWindow == null)
                        throw new InvalidOperationException( "mainWindow is null." );

                    var room = await Task.Run( () => RecordRoom.find( roomName ) );
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
            source.ForEach( (it) => uiRoomList.Add( new RecordRoom( it ) ) );
            lbRecord.ItemsSource = uiRoomList;
        }

        internal void showRecorderStatus() {
            var hub = mainWindow?.recorderHub;
            if (hub == null)
                return;

            foreach (var a in uiRoomList) {
                a.isRecordingUi = hub.getRecording( a.roomName )?.isRunning == true;
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

            tbRecordSaveDir.TextChanged += (sender, e) => checkRecordSaveDir();
            tbRecordFfmpegPath.TextChanged += (sender, e) => checkRecordFfmpegPath();

            // add event handler to bottom buttons
            btnCancel.Click += (sender, e) => Close();
            btnOk.Click += (sender, e) => { save(); Close(); };
            btnApply.Click += (sender, e) => { save(); updateApplyButton(); };

            btnRecordAdd.Click += (sender, e) => addRecord();

            checkRecordSaveDir();
            checkRecordFfmpegPath();
            showServerStatus();
            updateApplyButton();

            showRecorderStatus();
        }

        private static readonly HashSet<Char> invalidPathChars = new HashSet<Char>() { '*', '?', '"', '<', '>', '|' };

        private void checkRecordFfmpegPath() => Dispatcher.BeginInvoke( async () => {

            static String? check(String path) {
                var invalids = path.ToCharArray().Where( (c) => invalidPathChars.Contains( c ) ).ToList();
                if (invalids.Count > 0)
                    return "使用できない文字 " + String.Join( " ", invalids ) + " が含まれています。";
                if (!File.Exists( path ))
                    return "指定された場所にファイルが存在しません";
                return null;
            }

            static async Task readStream(StringBuilder dst, StreamReader reader) {
                try {
                    var tmp = new Char[ 4096 ];
                    while (true) {
                        var delta = await reader.ReadAsync( tmp, 0, tmp.Length ).ConfigureAwait( false );
                        if (delta <= 0)
                            break;
                        dst.Append( tmp, 0, delta );
                    }
                } catch (Exception ex) {
                    Log.e( ex, "readStream failed." );
                }
            }

            try {
                var path = tbRecordFfmpegPath.Text.Trim();

                var error = await Task.Run( () => check( path ) );
                if (error != null) {
                    throw new Exception( error );
                }

                var p = new Process();
                p.StartInfo.FileName = path;
                p.StartInfo.Arguments = "-hide_banner -version";
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();
                var t1 = Task.Run( async () => await readStream( sbOut, p.StandardOutput ) );
                var t2 = Task.Run( async () => await readStream( sbErr, p.StandardError ) );
                p.WaitForExit();
                await t1;
                await t2;
                var content = String.Join( "\n", sbOut, sbErr );
                content = new Regex( "[\\x0d\\x0a]+" ).Replace( content, "\x0d\x0a" );
                if (p.ExitCode == 0) {
                    content = new Regex( "[^\\x0d\\x0a]+" ).matchOrNull( content )?.Groups[ 0 ].Value ?? content;
                }
                tbRecordFfmpegPathError.Foreground = p.ExitCode == 0 ? Brushes.Blue : Brushes.Red;
                tbRecordFfmpegPathError.textOrGone( content );
                return;
            } catch (Exception ex) {
                tbRecordFfmpegPathError.Foreground = Brushes.Red;
                tbRecordFfmpegPathError.textOrGone( ex.Message );
                return;
            }
        } );

        private void checkRecordSaveDir() => Dispatcher.BeginInvoke( async () => {

            static String? checkFolderWriteable(String path) {
                var file = Path.Combine( path, ".checkWriteAccess" );
                try {
                    using var fh = File.Create( file );
                    return null;
                } catch (Exception ex) {
                    return $"checkFolderWriteable: {ex.Message}";
                } finally {
                    try {
                        File.Delete( file );
                    } catch (Exception) {
                        // ignored.
                    }
                }
            }

            static String? check(String path) {
                var invalids = path.ToCharArray().Where( (c) => invalidPathChars.Contains( c ) ).ToList();
                if (invalids.Count > 0)
                    return "使用できない文字 " + String.Join( " ", invalids ) + " が含まれています。";
                if (File.Exists( path ))
                    return "指定された場所には(フォルダではなく)ファイルが既に存在します。";
                if (!Directory.Exists( path ))
                    return "指定された場所にフォルダが存在しません。(たぶん自動作成されます)";
                return checkFolderWriteable( path );
            }

            try {
                var path = tbRecordSaveDir.Text.Trim();
                var error = await Task.Run( () => check( path ) );
                tbRecordSaveDirError.textOrGone( error ?? "" );
            } catch (Exception ex) {
                Log.e( ex, "checkRecordSaveDir() failed." );
                tbRecordSaveDirError.textOrGone( ex.Message ?? "checkRecordSaveDir() failed." );
            }
        } );
    }
}
