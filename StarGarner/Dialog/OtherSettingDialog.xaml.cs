using StarGarner.Model;
using StarGarner.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace StarGarner.Dialog {

    public partial class OtherSettingDialog : Window {

        public Boolean isClosed = false;

        private readonly MainWindow mainWindow;

        private SoundActor? selectedSoundActor => (SoundActor?)lbSoundActor.SelectedItem;

        void testSound()
          => selectedSoundActor?.test( mainWindow?.notificationSound, NotificationSound.allOther );

        internal void showServerStatus() => Dispatcher.BeginInvoke( (Action)( () => {
            if (isClosed)
                return;
            tbListenError.textOrGone( mainWindow?.httpServer.serverStatus ?? "" );
        } ) );

        //############################################################################

        private readonly ObservableCollection<RecordRoom> uiRecordRoomList = new ObservableCollection<RecordRoom>();

        private RecordRoom? selectedRecordRoom => (RecordRoom?)lbRecord.SelectedItem;

        private void lbRecord_View(Object sender, RoutedEventArgs e) {
            var room = selectedRecordRoom;
            if (room == null)
                return;

            var p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "cmd";
            p.StartInfo.Arguments = $"/C start {room.url}";
            p.Start();
        }

        private void lbRecord_Folder(Object sender, RoutedEventArgs e) {
            var room = selectedRecordRoom;
            if (room == null)
                return;

            Process.Start( "EXPLORER.EXE", Path.GetFullPath( room.getFolder( mainWindow!.recorderHub ) ).Replace( "/", "\\" ) );
        }

        private void lbRecord_Edit(Object sender, RoutedEventArgs e) {
            var room = selectedRecordRoom;
            if (room == null)
                return;

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

        private void lbRecord_Delete(Object sender, RoutedEventArgs e) {
            var i = lbRecord.SelectedIndex;
            if (i == -1)
                return;
            uiRecordRoomList.RemoveAt( i );
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
                    foreach (var r in uiRecordRoomList) {
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
                    foreach (var r in uiRecordRoomList) {
                        if (r.roomId == room.roomId) {
                            throw new DuplicateNameException( $"room_id {room.roomId} is duplicated. used in {r.roomName} and {room.roomName} ." );
                        }
                    }
                    var idx = uiRecordRoomList.InsertSorted( room );
                    lbRecord.SelectedIndex = idx;
                    updateApplyButton();
                    return null;
                } catch (Exception ex) {
                    Log.e( ex, "addRecord failed." );
                    return ex.Message;
                }
            }
        ).Show();

        private void loadRecordRoom() {
            var source = mainWindow.recorderHub.getRoomList();
            if (source == null)
                return;

            source.Sort();

            uiRecordRoomList.Clear();
            source.ForEach( (it) => uiRecordRoomList.Add( new RecordRoom( it ) ) );
            lbRecord.ItemsSource = uiRecordRoomList;
        }

        internal void showRecorderStatus() {
            if (isClosed)
                return;

            var hub = mainWindow.recorderHub;

            foreach (var a in uiRecordRoomList) {
                a.isRecordingUi = hub.getRecording( a.roomName )?.isRunning == true;
            }

            lbRecord.Items.Refresh();
        }

        //############################################################################

        private readonly ObservableCollection<CastRoom> uiCastRoomList = new ObservableCollection<CastRoom>();

        private CastRoom? selectedCastRoom => (CastRoom?)lbCast.SelectedItem;

        private void lbCast_View(Object sender, RoutedEventArgs e) {
            var room = selectedCastRoom;
            if (room == null)
                return;

            var p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "cmd";
            p.StartInfo.Arguments = $"/C start {room.url}";
            p.Start();
        }


        private void lbCast_Delete(Object sender, RoutedEventArgs e) {
            var i = lbCast.SelectedIndex;
            if (i == -1)
                return;
            uiCastRoomList.RemoveAt( i );
            updateApplyButton();
        }

        private void addCast() => new OneLineTextInputDialog(
            this,
            caption: $"投げる部屋のURL",
            initialValue: "",
            inputRestriction: OneLineTextInputDialog.InputStyle.RoomUrl,
            validator: (x) => {
                try {
                    var roomName = getRoomName( x );
                    foreach (var r in uiCastRoomList) {
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

                    var room = await Task.Run( () => CastRoom.find( roomName ) );
                    foreach (var r in uiCastRoomList) {
                        if (r.roomId == room.roomId) {
                            throw new DuplicateNameException( $"room_id {room.roomId} is duplicated. used in {r.roomName} and {room.roomName} ." );
                        }
                    }
                    var idx = uiCastRoomList.InsertSorted( room );
                    lbCast.SelectedIndex = idx;
                    updateApplyButton();
                    return null;
                } catch (Exception ex) {
                    Log.e( ex, "addCast failed." );
                    return ex.Message;
                }
            }
        ).Show();

        private void loadCastRoom() {
            var source = mainWindow.casterHub.getRoomList();
            if (source == null)
                return;

            source.Sort();

            uiCastRoomList.Clear();
            source.ForEach( (it) => uiCastRoomList.Add( new CastRoom( it ) ) );
            lbCast.ItemsSource = uiCastRoomList;
        }

        internal void showCasterStatus() {
            if (isClosed)
                return;

            var hub = mainWindow.casterHub;

            foreach (var a in uiCastRoomList) {
                a.isCastingUi = hub.getCasting( a.roomName )?.isCasting == true;
            }

            lbCast.Items.Refresh();
        }

        //############################################################################

        private static readonly HashSet<Char> invalidPathChars = new HashSet<Char>() { '*', '?', '"', '<', '>', '|' };

        internal static String? checkFileExists(String path) {
            var invalids = path.ToCharArray().Where( (c) => invalidPathChars.Contains( c ) ).ToList();
            if (invalids.Count > 0)
                return "使用できない文字 " + String.Join( " ", invalids ) + " が含まれています。";
            if (!File.Exists( path ))
                return "指定された場所にファイルが存在しません";
            return null;
        }

        internal static async Task readStream(StringBuilder dst, StreamReader reader) {
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

        private void checkRecordFfmpegPath() => Dispatcher.BeginInvoke( async () => {
            try {
                if (isClosed)
                    return;

                var path = tbRecordFfmpegPath.Text.Trim();

                var error = await Task.Run( () => checkFileExists( path ) );
                if (error != null) {
                    throw new Exception( error );
                }

                var p = new Process();
                p.StartInfo.FileName = path;
                p.StartInfo.Arguments = "-hide_banner -version";
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = true;

                var taskCompletionSource = new TaskCompletionSource<Boolean>();
                p.Exited += (sender, e) => taskCompletionSource.SetResult( false );

                p.Start();

                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();
                var t1 = Task.Run( async () => await readStream( sbOut, p.StandardOutput ) );
                var t2 = Task.Run( async () => await readStream( sbErr, p.StandardError ) );

                await taskCompletionSource.Task;
                await t1;
                await t2;

                var content = String.Join( "\n", sbOut, sbErr );
                content = new Regex( "[\\x0d\\x0a]+" ).Replace( content, "\x0d\x0a" );
                if (p.ExitCode == 0) {
                    content = new Regex( "[^\\x0d\\x0a]+" ).matchOrNull( content )?.Groups[ 0 ].Value ?? content;
                }

                tbRecordFfmpegPathError.Foreground = p.ExitCode == 0 ? Brushes.Blue : Brushes.Red;
                tbRecordFfmpegPathError.textOrGone( content );
            } catch (Exception ex) {
                Log.e( ex, "checkRecordFfmpegPath() failed." );
                tbRecordFfmpegPathError.Foreground = Brushes.Red;
                tbRecordFfmpegPathError.textOrGone( ex.Message );
            }
        } );

        internal static String? checkFolderWriteable(String path) {
            var file = Path.Combine( path, ".checkWriteAccess" );
            try {
                using var fh = File.Create( file );
                return null;
            } catch (Exception ex) {
                return $"フォルダへの書き込み権限がないようです。 {ex.Message}";
            } finally {
                try {
                    File.Delete( file );
                } catch (Exception) {
                    // ignored.
                }
            }
        }

        internal static String? checkDirectory(String path) {
            var invalids = path.ToCharArray().Where( (c) => invalidPathChars.Contains( c ) ).ToList();
            if (invalids.Count > 0)
                return "使用できない文字 " + String.Join( " ", invalids ) + " が含まれています。";
            if (File.Exists( path ))
                return "指定された場所には(フォルダではなく)ファイルが既に存在します。";
            if (!Directory.Exists( path ))
                return "指定された場所にフォルダが存在しません。(たぶん自動作成されます)";
            return checkFolderWriteable( path );
        }

        private void checkRecordSaveDir() => Dispatcher.BeginInvoke( async () => {
            try {
                if (isClosed)
                    return;

                var path = tbRecordSaveDir.Text.Trim();
                var error = await Task.Run( () => checkDirectory( path ) );
                tbRecordSaveDirError.textOrGone( error ?? "" );
            } catch (Exception ex) {
                Log.e( ex, "checkRecordSaveDir() failed." );
                tbRecordSaveDirError.textOrGone( ex.Message ?? "checkRecordSaveDir() failed." );
            }
        } );

        //############################################################################

        private Boolean isChanged() => Utils.or(
            MyResourceRequestHandler.responseLogEnabled != ( cbResponseLog.IsChecked ?? false )
            , mainWindow.httpServer.enabled != ( cbListen.IsChecked ?? false )
            , mainWindow.httpServer.listenAddr != tbListenAddress.Text.Trim()
            , mainWindow.httpServer.listenPort != tbListenPort.Text.Trim()
            , mainWindow.recorderHub.saveDir != tbRecordSaveDir.Text.Trim()
            , mainWindow.recorderHub.ffmpegPath != tbRecordFfmpegPath.Text.Trim()
            , mainWindow.recorderHub.isRoomListChanged( uiRecordRoomList )
            , mainWindow.casterHub.isRoomListChanged( uiCastRoomList)
            , mainWindow.soundActor != selectedSoundActor?.Name
            );

        private void updateApplyButton() => btnApply.IsEnabled = isChanged();

        void save() {
            if (!isChanged())
                return;

            var st = selectedSoundActor;
            if (st != null)
                mainWindow.soundActor = st.Name;

            MyResourceRequestHandler.responseLogEnabled = cbResponseLog.IsChecked ?? false;

            mainWindow.httpServer.enabled = cbListen.IsChecked ?? false;
            mainWindow.httpServer.listenAddr = tbListenAddress.Text.Trim();
            mainWindow.httpServer.listenPort = tbListenPort.Text.Trim();
            mainWindow.httpServer.updateListening();

            mainWindow.recorderHub.saveDir = tbRecordSaveDir.Text.Trim();
            mainWindow.recorderHub.ffmpegPath = tbRecordFfmpegPath.Text.Trim();
            mainWindow.recorderHub.setRoomList( uiRecordRoomList );

            mainWindow.casterHub.setRoomList( uiCastRoomList);

            mainWindow.saveOtherSetting();
        }

        //############################################################################

        protected override void OnClosed(EventArgs e) {
            isClosed = true;
            base.OnClosed( e );
        }

        public OtherSettingDialog(MainWindow mainWindow) {
            this.mainWindow = mainWindow;
            this.Owner = mainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.SourceInitialized += (x, y) => this.HideMinimizeAndMaximizeButtons();

            InitializeComponent();

            // load ui value
            cbResponseLog.IsChecked = MyResourceRequestHandler.responseLogEnabled;
            cbListen.IsChecked = mainWindow.httpServer.enabled;
            tbListenAddress.Text = mainWindow.httpServer.listenAddr;
            tbListenPort.Text = mainWindow.httpServer.listenPort;
            tbRecordSaveDir.Text = mainWindow.recorderHub.saveDir;
            tbRecordFfmpegPath.Text = mainWindow.recorderHub.ffmpegPath;
            loadRecordRoom();
            loadCastRoom();

            // add event handler
            cbResponseLog.Checked += (sender, e) => updateApplyButton();
            cbResponseLog.Unchecked += (sender, e) => updateApplyButton();
            cbListen.Checked += (sender, e) => updateApplyButton();
            cbListen.Unchecked += (sender, e) => updateApplyButton();
            tbListenAddress.TextChanged += (sender, e) => updateApplyButton();
            tbListenPort.TextChanged += (sender, e) => updateApplyButton();

            tbRecordSaveDir.TextChanged += (sender, e) => {
                updateApplyButton();
                checkRecordSaveDir();
            };

            tbRecordFfmpegPath.TextChanged += (sender, e) => {
                updateApplyButton();
                checkRecordFfmpegPath();
            };

            // add event handler to bottom buttons
            btnCancel.Click += (sender, e) => Close();
            btnOk.Click += (sender, e) => { save(); Close(); };
            btnApply.Click += (sender, e) => { save(); updateApplyButton(); };

            btnRecordAdd.Click += (sender, e) => addRecord();

            btnCastAdd.Click += (sender, e) => addCast();

            SoundActor.initListBox( lbSoundActor, mainWindow.soundActor );
            lbSoundActor.SelectionChanged += (sender, e) => updateApplyButton();
            btnTestSoundActor.Click += (sender, e) => testSound();

            checkRecordSaveDir();
            checkRecordFfmpegPath();
            showServerStatus();
            updateApplyButton();

            showRecorderStatus();
            showCasterStatus();
        }
    }
}
