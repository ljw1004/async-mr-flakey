Imports Microsoft.VisualBasic
Imports Windows.UI

Namespace Global
    Public Module MrFlakey

        Private isFlakey As Boolean
        Private incs As New HashSet(Of TaskInfo)
        Private comps As New Queue(Of TaskInfo)
        Private _lock As New System.Threading.SemaphoreSlim(1)
        Private _listChange As New TaskCompletionSource(Of Boolean)
        Private _buttonChange As New TaskCompletionSource(Of FrameworkElement)

        Private Class TaskInfo
            Public s1, s2 As String
            Public t As Task
            Public fail As Action
            Public ok As Action
        End Class

        Public Async Sub Start()
            If isFlakey Then Return Else isFlakey = True
            Dim p As New Controls.Primitives.Popup With {.VerticalOffset = Window.Current.Bounds.Height - 70, .Width = Window.Current.Bounds.Width, .Height = 70}
            Dim g As New Grid With {.Width = Window.Current.Bounds.Width, .Height = 70, .Background = New SolidColorBrush(Color.FromArgb(255, 0, 0, 128))}
            p.Child = g
            g.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
            g.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Auto)})
            g.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Auto)})
            Dim sp0 As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
            Dim tbA As New TextBlock With {.Text = "s1", .Foreground = New SolidColorBrush(Colors.White), .FontSize = 14}
            Dim tbB As New TextBlock With {.Text = "s2", .Foreground = New SolidColorBrush(Colors.White), .FontSize = 14}
            Dim sp1 As New StackPanel With {.Orientation = Orientation.Horizontal, .Background = New SolidColorBrush(Colors.Green)}
            Dim bok As New Button With {.Content = "ok", .MinWidth = 30}
            Dim xok As New CheckBox With {.MinWidth = 30, .VerticalAlignment = VerticalAlignment.Center}
            Dim sp2 As New StackPanel With {.Orientation = Orientation.Horizontal, .Background = New SolidColorBrush(Colors.Red)}
            Dim bfail As New Button With {.Content = "fail", .MinWidth = 30}
            Dim xfail As New CheckBox With {.MinWidth = 30, .VerticalAlignment = VerticalAlignment.Center}
            Grid.SetColumn(sp0, 0)
            Grid.SetColumn(sp1, 1)
            Grid.SetColumn(sp2, 2)
            sp0.Children.Add(tbA) : sp0.Children.Add(tbB)
            sp1.Children.Add(bok) : sp1.Children.Add(xok)
            sp2.Children.Add(bfail) : sp2.Children.Add(xfail)
            g.Children.Add(sp0) : g.Children.Add(sp1) : g.Children.Add(sp2)
            AddHandler bok.Click, Sub(s, e) _buttonChange.TrySetResult(bok)
            AddHandler bfail.Click, Sub(s, e) _buttonChange.TrySetResult(bfail)
            AddHandler xok.Click, Sub(s, e) _buttonChange.TrySetResult(xok)
            AddHandler xfail.Click, Sub(s, e) _buttonChange.TrySetResult(xfail)
            p.IsOpen = True

            Dim sentinelTask As Task = (New TaskCompletionSource(Of Boolean)).Task

            While True
                ' Update the display
                Dim ti2 = If(comps.Count > 0, comps.Peek(), If(incs.Count > 0, incs.First(), Nothing))
                tbA.Text = If(ti2 Is Nothing, "", ti2.s1 & "    [" & comps.Count & ":" & incs.Count & "]")
                tbB.Text = If(ti2 Is Nothing, "", ti2.s2)

                ' Enable/disable UI elements
                xfail.IsEnabled = (Not xok.IsChecked.Value)
                xok.IsEnabled = (Not xfail.IsChecked.Value)
                bfail.IsEnabled = (Not xfail.IsChecked.Value AndAlso Not xok.IsChecked.Value AndAlso (comps.Count > 0 OrElse incs.Count > 0))
                bok.IsEnabled = (Not xfail.IsChecked.Value AndAlso Not xok.IsChecked.Value AndAlso comps.Count > 0)

                ' Wait for an event
                Dim list_task = _listChange.Task
                Dim btn_task = _buttonChange.Task
                Dim tt = incs.Select(Function(ti) ti.t).ToArray()
                Dim comp_task = If(tt.Count > 0, Task.WhenAny(tt), sentinelTask)
                Dim winner = Await Task.WhenAny(list_task, btn_task, comp_task)

                ' Reset any events
                If winner Is list_task Then _listChange = New TaskCompletionSource(Of Boolean)
                If winner Is btn_task Then _buttonChange = New TaskCompletionSource(Of FrameworkElement)

                ' Analyze the event that just happened
                Dim btn = If(winner Is btn_task, btn_task.GetAwaiter().GetResult(), Nothing)
                If winner Is comp_task Then
                    Try
                        Await _lock.WaitAsync()
                        Dim newcomps = (From ti In incs Where ti.t.IsCompleted).ToArray()
                        incs.RemoveWhere(Function(ti) newcomps.Contains(ti))
                        For Each ti1 In newcomps : comps.Enqueue(ti1) : Next
                    Finally
                        _lock.Release()
                    End Try
                End If

                ' Process any necessary actions
                Dim fail = Function()
                               If comps.Count > 0 Then
                                   Dim ti = comps.Dequeue() : ti.fail()
                               ElseIf incs.Count > 0 Then
                                   Dim ti = incs.First() : ti.fail() : incs.Remove(ti)
                               Else
                                   Return False
                               End If
                               Return True
                           End Function
                Dim ok = Function()
                             If comps.Count > 0 Then
                                 Dim ti = comps.Dequeue() : ti.ok()
                             Else
                                 Return False
                             End If
                             Return True
                         End Function

                If xfail.IsChecked Then
                    While fail() : End While
                ElseIf xok.IsChecked Then
                    While ok() : End While
                ElseIf btn Is bfail Then
                    fail()
                ElseIf btn Is bok Then
                    ok()
                End If

            End While
        End Sub

        <Runtime.CompilerServices.Extension>
        Public Async Function Flakey(Of T)(task As Task(Of T), Optional name As String = "",
                                  <Runtime.CompilerServices.CallerFilePath> Optional file As String = "",
                                  <Runtime.CompilerServices.CallerMemberName> Optional member As String = "") As Task(Of T)
            If Not isFlakey Then Return Await task

            Dim tcs As New TaskCompletionSource(Of T)
            Dim ti As New TaskInfo With {.s1 = name, .s2 = member & " [" & IO.Path.GetFileName(file) & "]", .t = task,
                                         .ok = Sub()
                                                   Try
                                                       tcs.TrySetResult(task.GetAwaiter().GetResult())
                                                   Catch ex As Exception
                                                       tcs.TrySetException(ex)
                                                   End Try
                                               End Sub,
                                         .fail = Sub() tcs.TrySetException(New IO.IOException("flakey"))}
            Try
                Await _lock.WaitAsync()
                If task.IsCompleted Then comps.Enqueue(ti) Else incs.Add(ti)
            Finally
                _lock.Release()
            End Try
            _listChange.TrySetResult(True)
            Return Await tcs.Task
        End Function

        <Runtime.CompilerServices.Extension>
        Public Async Function Flakey(task As Task, Optional name As String = "",
                            <Runtime.CompilerServices.CallerFilePath> Optional file As String = "",
                            <Runtime.CompilerServices.CallerMemberName> Optional member As String = "") As Task
            If Not isFlakey Then Await task : Return


            Dim tcs As New TaskCompletionSource(Of Object)
            Dim ti As New TaskInfo With {.s1 = name, .s2 = member & " [" & IO.Path.GetFileName(file) & "]", .t = task,
                                         .ok = Sub() tcs.TrySetResult(Nothing),
                                         .fail = Sub() tcs.TrySetException(New IO.IOException("flakey"))}
            Try
                Await _lock.WaitAsync()
                If task.IsCompleted Then comps.Enqueue(ti) Else incs.Add(ti)
            Finally
                _lock.Release()
            End Try
            _listChange.TrySetResult(True)
            Await tcs.Task
        End Function

    End Module
End Namespace
