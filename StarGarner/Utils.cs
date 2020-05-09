using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace StarGarner {
    internal static class Utils {

        internal static SingleTask singleTask = new SingleTask();

        internal static void saveTo(this JToken data, String fileName) {
            var str = data.ToString( Formatting.None );
            singleTask.add( () => {
                try {
                    using var writer = new StreamWriter( fileName, false, Encoding.UTF8 );
                    writer.Write( str );
                } catch (Exception ex) {
                    Log.e( ex, $"{fileName} save failed." );
                }
            } );
        }

        internal static JToken loadJson(String fileName) {
            using var reader = new StreamReader( fileName, Encoding.UTF8 );
            var str = reader.ReadToEnd();
            return JToken.Parse( str );
        }

        public static void dumpTo(this InlineCollection list, StringBuilder sb) {
            foreach (var y in list) {
                y.dumpTo( sb );
            }
        }

        public static void dumpTo(this Inline item, StringBuilder sb) {
            if (item is Run r)
                sb.Append( r.Text );
            else if (item is Hyperlink h)
                h.Inlines.dumpTo( sb );
        }

        public static Int32? notZero(this Int32 v) => v != 0 ? v : (Int32?)null;

#nullable disable
        public static T elementOrNull<T>(this List<T> list, Int32 index) {
            try {
                return list[ index ];
            } catch (Exception) {
                return default;
            }
        }
        public static T firstOrNull<T>(this List<T> list) => elementOrNull( list, 0 );
        public static T lastOrNull<T>(this List<T> list) => elementOrNull( list, list.Count - 1 );
#nullable enable


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
}
