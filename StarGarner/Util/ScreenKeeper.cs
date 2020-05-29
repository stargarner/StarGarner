using System;
using System.Runtime.InteropServices;

namespace StarGarner.Util {
    internal class ScreenKeeper {
        static readonly Log log = new Log( "ScreenKeeper" );

        [StructLayout( LayoutKind.Sequential )]
        struct MouseInput {
            public Int32 X;
            public Int32 Y;
            public Int32 Data;
            public Int32 Flags;
            public Int32 Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout( LayoutKind.Sequential )]
        struct KeyboardInput {
            public Int16 VirtualKey;
            public Int16 ScanCode;
            public Int32 Flags;
            public Int32 Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout( LayoutKind.Sequential )]
        struct HardwareInput {
            public Int32 uMsg;
            public Int16 wParamL;
            public Int16 wParamH;
        }

        [StructLayout( LayoutKind.Explicit )]
        struct InputUnion {
            [FieldOffset( 0 )]
            public MouseInput Mouse;
            [FieldOffset( 0 )]
            public KeyboardInput Keyboard;
            [FieldOffset( 0 )]
            public HardwareInput Hardware;
        }

        [StructLayout( LayoutKind.Sequential )]
        struct Input {
            public Int32 Type;
            public InputUnion ui;
        }

        // スタンバイ状態にするのを防ぐ
        private const UInt32 ES_SYSTEM_REQUIRED = 0x00000001;

        // ディスプレイをオフにするのを防ぐ
        private const UInt32 ES_DISPLAY_REQUIRED = 0x00000002;

        // 実行状態を維持する 
        private const UInt32 ES_CONTINUOUS = 0x80000000;

        const Int32 INPUT_MOUSE = 0;
        const Int32 MOUSEEVENTF_MOVE = 1;

        [DllImport( "kernel32.dll" )]
        static extern UInt32 SetThreadExecutionState(UInt32 esFlags);

        [DllImport( "user32.dll", SetLastError = true )]
        extern static UInt32 SendInput(Int32 nInputs, ref Input pInputs, Int32 cbsize);

        [DllImport( "user32.dll", SetLastError = true )]
        extern static IntPtr GetMessageExtraInfo();

        // CPUのスタンバイを禁止
        public static UInt32 DisableSuspend()
            => SetThreadExecutionState( ES_SYSTEM_REQUIRED | ES_CONTINUOUS );

        // CPUのスタンバイを許可
        public static UInt32 EnableSuspend()
            => SetThreadExecutionState( ES_CONTINUOUS );


        private Int64 lastSupressMonitorOff;

        // 定期的に呼び出すこと。58秒ごとにスクリーンセーバー抑止とディスプレイOFF抑止を祈願する
        public void suppressMonitorOff(Int64 now) {
            if (now - lastSupressMonitorOff < 58000L)
                return;
            lastSupressMonitorOff = now;

            // 移動量0のマウスイベントを生成してスクリーンセーバーを抑止
            var input = new Input {
                Type = INPUT_MOUSE
            };
            input.ui.Mouse.Flags = MOUSEEVENTF_MOVE;
            input.ui.Mouse.Data = 0;
            input.ui.Mouse.X = 0;
            input.ui.Mouse.Y = 0;
            input.ui.Mouse.Time = 0;
            input.ui.Mouse.ExtraInfo = GetMessageExtraInfo();
            var rv = SendInput( 1, ref input, Marshal.SizeOf( input ) );
            if (rv != 1)
                log.e( $"suppressMonitorOff: SendInput failed. {rv}" );


            // モニターの電源OFFの抑止
            rv = SetThreadExecutionState( ES_DISPLAY_REQUIRED );
            if (rv != 0x80000001)
                log.e( $"suppressMonitorOff: SetThreadExecutionState failed. {rv}" );

        }
    }
}
