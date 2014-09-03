using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

// This C# code isn't used by any project here,
// nor by the "AsyncMrFlakey" portable class library nor NuGet.
// It's an exact translation of the VB code.
// I'm including it just in case anyone wants to read through the C#,
// or incorporate it directly into their project.

public static class MrFlakey
{
    private static bool IsFlakey;
    private static HashSet<TaskInfo> incs = new HashSet<TaskInfo>();
    private static Queue<TaskInfo> comps = new Queue<TaskInfo>();
    private static SemaphoreSlim _lock = new SemaphoreSlim(1);
    private static TaskCompletionSource<bool> _listChange = new TaskCompletionSource<bool>();
    private static TaskCompletionSource<FrameworkElement> _buttonChange = new TaskCompletionSource<FrameworkElement>();
    private class TaskInfo
    {
        public string s1, s2;
        public Task t;
        public Action fail, ok;
    }

    public static async void Start()
    {
        if (IsFlakey) return; else IsFlakey = true;
        var p = new Popup { VerticalOffset = Window.Current.Bounds.Height - 70, Width = Window.Current.Bounds.Width, Height = 70 };
        var g = new Grid { Width = Window.Current.Bounds.Width, Height = 70, Background = new SolidColorBrush(Color.FromArgb(255, 0, 0, 128)) };
        p.Child = g;
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        var sp0 = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var tbA = new TextBlock { Text = "s1", Foreground = new SolidColorBrush(Colors.White), FontSize = 14 };
        var tbB = new TextBlock { Text = "s2", Foreground = new SolidColorBrush(Colors.White), FontSize = 14 };
        var sp1 = new StackPanel { Orientation = Orientation.Horizontal, Background = new SolidColorBrush(Colors.Green) };
        var bok = new Button { Content = "ok", MinWidth = 30 };
        var xok = new CheckBox { MinWidth = 30, VerticalAlignment = VerticalAlignment.Center };
        var sp2 = new StackPanel { Orientation = Orientation.Horizontal, Background = new SolidColorBrush(Colors.Red) };
        var bfail = new Button { Content = "fail", MinWidth = 30 };
        var xfail = new CheckBox { MinWidth = 30, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(sp0, 0);
        Grid.SetColumn(sp1, 1);
        Grid.SetColumn(sp2, 2);
        sp0.Children.Add(tbA); sp0.Children.Add(tbB);
        sp1.Children.Add(bok); sp1.Children.Add(xok);
        sp2.Children.Add(bfail); sp2.Children.Add(xfail);
        g.Children.Add(sp0); g.Children.Add(sp1); g.Children.Add(sp2);
        bok.Click += (s, e) => _buttonChange.TrySetResult(bok);
        bfail.Click += (s, e) => _buttonChange.TrySetResult(bfail);
        xok.Click += (s, e) => _buttonChange.TrySetResult(xok);
        xfail.Click += (s, e) => _buttonChange.TrySetResult(xfail);
        g.LayoutUpdated += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine(g.ColumnDefinitions[0].ActualWidth);
                System.Diagnostics.Debug.WriteLine(g.ColumnDefinitions[1].ActualWidth);
                System.Diagnostics.Debug.WriteLine(g.ColumnDefinitions[2].ActualWidth);
                System.Diagnostics.Debug.WriteLine("oops");
            };
        p.IsOpen = true;

        var sentinelTask = (new TaskCompletionSource<bool>().Task) as Task;
        while (true)
        {
            // Update the display
            var ti2 = comps.Count > 0 ? comps.Peek() : (incs.Count > 0 ? incs.First() : null);
            tbA.Text = ti2 == null ? "" : ti2.s1 + "    [" + comps.Count + ":" + incs.Count + "]";
            tbB.Text = ti2 == null ? "" : ti2.s2;

            // Enable/disable UI elements
            xfail.IsEnabled = !xok.IsChecked.Value;
            xok.IsEnabled = !xfail.IsChecked.Value;
            bfail.IsEnabled = (!xfail.IsChecked.Value && !xok.IsChecked.Value && (comps.Count > 0 || incs.Count > 0));
            bok.IsEnabled = (!xfail.IsChecked.Value && !xok.IsChecked.Value && comps.Count > 0);

            // Wait for an event
            var list_task = _listChange.Task;
            var btn_task = _buttonChange.Task;
            var tt = incs.Select(ti => ti.t).ToArray();
            var comp_task = tt.Count() > 0 ? Task.WhenAny(tt) : sentinelTask;
            var winner = await Task.WhenAny(list_task, btn_task, comp_task);

            // Reset any events
            if (winner == list_task) _listChange = new TaskCompletionSource<bool>();
            if (winner == btn_task) _buttonChange = new TaskCompletionSource<FrameworkElement>();

            // Analyze the event that just happened
            var btn = winner == btn_task ? btn_task.GetAwaiter().GetResult() : null;
            if (winner == comp_task)
            {
                try
                {
                    await _lock.WaitAsync();
                    var newcomps = incs.Where(ti => ti.t.IsCompleted).ToArray();
                    incs.RemoveWhere(ti => newcomps.Contains(ti));
                    foreach (var ti1 in newcomps) comps.Enqueue(ti1);
                }
                finally
                {
                    _lock.Release();
                }
            }

            // Process any necessary actions
            Func<bool> fail = () =>
            {
                if (comps.Count > 0) { var ti = comps.Dequeue(); ti.fail(); }
                else if (incs.Count > 0) { var ti = incs.First(); ti.fail(); incs.Remove(ti); }
                else { return false; }
                return true;
            };
            Func<bool> ok = () =>
            {
                if (comps.Count > 0) { var ti = comps.Dequeue(); ti.ok(); }
                else { return false; }
                return true;
            };

            if (xfail.IsChecked.Value) { while (fail()) { } }
            else if (xok.IsChecked.Value) { while (ok()) { } }
            else if (btn == bfail) fail();
            else if (btn == bok) ok();
        }

    }

    public static async Task<T> Flakey<T>(this Task<T> task, string name = "", [CallerFilePath] string file = "", [CallerMemberName] string member = "")
    {
        if (!IsFlakey) return await task;

        var tcs = new TaskCompletionSource<T>();
        var ti = new TaskInfo
        {
            s1 = name,
            s2 = member + " [" + Path.GetFileName(file) + "]",
            t = task,
            ok = delegate { try { tcs.TrySetResult(task.GetAwaiter().GetResult()); } catch (Exception ex) { tcs.TrySetException(ex); } },
            fail = delegate { tcs.TrySetException(new IOException("flakey")); }
        };
        try
        {
            await _lock.WaitAsync();
            if (task.IsCompleted) comps.Enqueue(ti); else incs.Add(ti);
        }
        finally
        {
            _lock.Release();
        }
        _listChange.TrySetResult(true);
        return await tcs.Task;
    }


    public static async Task Flakey(this Task task, string name = "", [CallerFilePath] string file = "", [CallerMemberName] string member = "")
    {
        if (!IsFlakey) { await task; return; }

        var tcs = new TaskCompletionSource<object>();
        var ti = new TaskInfo
        {
            s1 = name,
            s2 = member + " [" + Path.GetFileName(file) + "]",
            t = task,
            ok = delegate { tcs.TrySetResult(null); },
            fail = delegate { tcs.TrySetException(new IOException("flakey")); }
        };
        try
        {
            await _lock.WaitAsync();
            if (task.IsCompleted) comps.Enqueue(ti); else incs.Add(ti);
        }
        finally
        {
            _lock.Release();
        }
        _listChange.TrySetResult(true);
        await tcs.Task;
    }
}
