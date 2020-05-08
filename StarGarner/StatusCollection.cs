using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace StarGarner {
    public class StatusCollection {
        private readonly List<Inline> list = new List<Inline>();

        public void newLine() {
            if (list.Count > 0)
                list.Add( new Run() {
                    Text = "\n"
                } );
        }

        public void addRun(String text, Double? fontSize = null) {
            if (fontSize.HasValue) {
                list.Add( new Run() {
                    Text = text,
                    FontSize = fontSize.Value
                } );
            } else {
                list.Add( new Run() {
                    Text = text
                } );
            }
        }

        public void add(String line, Double? fontSize = null) {
            foreach (var item in list) {
                if (item is Run r && r.Text == line)
                    return;
            }
            newLine();
            addRun( line, fontSize );
        }

        public void addLink(String line, Hyperlink link) {
            foreach (var item in list) {
                if (item is Run r && r.Text == line)
                    return;
            }
            if (list.Count > 0)
                list.Add( new Run() { Text = "\n" } );
            list.Add( new Run() { Text = line } );
            list.Add( new Run() { Text = " " } );
            list.Add( link );
        }

        public void setTo(TextBlock tb) {
            tb.Inlines.Clear();
            foreach (var item in list) {
                tb.Inlines.Add( item );
            }
            tb.Visibility = list.Count switch
            {
                0 => Visibility.Collapsed,
                _ => Visibility.Visible
            };
        }

        public override String ToString() {
            var sb = new StringBuilder();
            list.ForEach( x => x.dumpTo( sb ) );
            return sb.ToString();
        }

        public static void textOrGone(TextBlock tb, String str) {
            tb.Text = str;
            tb.Visibility = str.Length switch
            {
                0 => Visibility.Collapsed,
                _ => Visibility.Visible
            };
        }
    }
}
