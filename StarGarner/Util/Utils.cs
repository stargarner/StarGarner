using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using uhttpsharp;
using uhttpsharp.Handlers;

namespace StarGarner.Util {
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
                return ( index < 0 || index >= list.Count ) ? default : list[ index ];
            } catch (Exception) {
                return default;
            }
        }
        public static T firstOrNull<T>(this List<T> list) => list.elementOrNull( 0 );
        public static T lastOrNull<T>(this List<T> list) => list.elementOrNull( list.Count - 1 );

        public static T getOrNull<T>(this WeakReference<T> wr) where T : class {
            wr.TryGetTarget( out var d );
            return d;
        }

        public static V getOrNull<K, V>(this ConcurrentDictionary<K, V> map, K key) {
            map.TryGetValue( key, out var v );
            return v;
        }

        public static V removeOrNull<K, V>(this ConcurrentDictionary<K, V> map, K key) {
            map.TryRemove( key, out var v );
            return v;
        }

        public static void textOrGone(this TextBlock tb, String str) {
            tb.Text = str;
            tb.Visibility = str.Length switch
            {
                0 => Visibility.Collapsed,
                _ => Visibility.Visible
            };
        }

        public static void textOrGone(this TextBox tb, String str) {
            tb.Text = str;
            tb.Visibility = str.Length switch
            {
                0 => Visibility.Collapsed,
                _ => Visibility.Visible
            };
        }
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

        public class FooHandler : IHttpRequestHandler {
            readonly Func<IHttpContext, Task> handler;

            public FooHandler(Func<IHttpContext, Task> handler) => this.handler = handler;

            public Task Handle(IHttpContext context, Func<Task> next) => handler( context );
        }

        public static void With(this HttpRouter router, String path, Func<IHttpContext, Task> action)
            => router.With( path, new FooHandler( action ) );

        public static Task RunAsync(this Dispatcher dispatcher, Action action) {
            var taskCompletionSource = new TaskCompletionSource<Boolean>();
            dispatcher.BeginInvoke( () => {
                try {
                    action();
                    taskCompletionSource.SetResult( false );
                } catch (Exception ex) {
                    taskCompletionSource.SetException( ex );
                }
            } );
            return taskCompletionSource.Task;
        }

        public static Task<T> RunAsync<T>(this Dispatcher dispatcher, Func<T> action) {
            var taskCompletionSource = new TaskCompletionSource<T>();
            dispatcher.BeginInvoke( () => {
                try {
                    taskCompletionSource.SetResult( action() );
                } catch (Exception ex) {
                    taskCompletionSource.SetException( ex );
                }
            } );
            return taskCompletionSource.Task;
        }


        public static Int32 InsertSorted<T>(this ObservableCollection<T> collection, T item) where T : IComparable<T> {
            for (Int32 i = 0, ie = collection.Count; i < ie; i++) {
                var result = collection[ i ].CompareTo( item );
                if (result == 0) {
                    throw new DuplicateNameException( "既に登録されています" );
                } else if (result > 0) {
                    collection.Insert( i, item );
                    return i;
                }
            }
            collection.Add( item );
            return collection.Count - 1;
        }

        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> action) {
            foreach (var item in sequence)
                action( item );
        }

        public static Match? matchOrNull(this Regex re, String input) {
            var m = re.Match( input );
            return m.Success ? m : null;
        }

        public static Int32 toInt32(this String src) => Int32.Parse( src );

        public static Int64 toInt64(this String src) => Int64.Parse( src );

        public static String decodeEntity(this String src)
            => WebUtility.HtmlDecode( src );

        // |や&で連結するより , で区切る方が演算子の優先順位の問題が少ない
        public static Boolean or(params Boolean[] values) 
            => values.Where( (it) => it ).Any();

        public static Boolean and(params Boolean[] values)
            => ! values.Where( (it) => !it ).Any();
    }
}
