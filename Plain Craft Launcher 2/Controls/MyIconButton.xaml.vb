Imports System.Windows.Shapes

Public Class MyIconButton

    '自定义事件
    Public Event Click(sender As Object, e As EventArgs)

    '自定义属性

    Public Uuid As Integer = GetUuid()
    Public Property Logo As String
        Get
            Return Path.Data.ToString
        End Get
        Set(value As String)
            Path.Data = (New GeometryConverter).ConvertFromString(value)
        End Set
    End Property

    Private _LogoScale As Double = 1
    Public Property LogoScale() As Double
        Get
            Return _LogoScale
        End Get
        Set(value As Double)
            _LogoScale = value
            If Not IsNothing(Path) Then Path.RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale}
        End Set
    End Property

    '是否以描边（而非填充）渲染图标；描边粗细见 LogoStrokeThickness（默认与最小化按钮一致为 2）
    Public Property LogoStroke As Boolean = False
    Private _LogoStrokeThickness As Double = 2
    Public Property LogoStrokeThickness() As Double
        Get
            Return _LogoStrokeThickness
        End Get
        Set(value As Double)
            _LogoStrokeThickness = value
            If Not IsNothing(Path) Then Path.StrokeThickness = value
        End Set
    End Property

    Public Enum Themes
        Color
        White
        Black
        Red
        Custom
    End Enum
    Public Property Theme As Themes = Themes.Color

    Private _Foreground As New SolidColorBrush(Color.FromRgb(128, 128, 128))
    Public Property Foreground As SolidColorBrush
        Get
            Return _Foreground
        End Get
        Set(value As SolidColorBrush)
            _Foreground = value
            AniControlEnabled += 1
            RefreshAnim()
            AniControlEnabled -= 1
        End Set
    End Property

    '鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    Private IsMouseDown As Boolean = False
    Public Property CanClick As Boolean = True
    Private Sub Button_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonUp
        If Not CanClick Then Return
        If Not IsMouseDown Then Return
        Logger.Info($"按下图标按钮{If(String.IsNullOrEmpty(Name), "", "：" & Name)}")
        RaiseEvent Click(sender, e)
        e.Handled = True
        Button_MouseUp()
        RaiseCustomEvent() '自定义事件
    End Sub
    Private Sub Button_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonDown
        If Not CanClick Then Return
        IsMouseDown = True
        Focus()
        '指向
        AniStart(AaScaleTransform(PanBack, 0.8 - CType(PanBack.RenderTransform, ScaleTransform).ScaleX, 400,, New AniEaseOutFluent(AniEasePower.Strong)), "MyIconButton Scale " & Uuid)
    End Sub
    Private Sub Button_MouseUp() Handles Me.MouseLeftButtonUp
        If Not CanClick Then Return
        If IsMouseDown Then
            IsMouseDown = False
            AniStart({
                     AaScaleTransform(PanBack, 1.05 - CType(PanBack.RenderTransform, ScaleTransform).ScaleX, 250,, New AniEaseOutBack(AniEasePower.Weak)),
                     AaScaleTransform(PanBack, -0.05, 250,, New AniEaseOutFluent(AniEasePower.Strong))
                 }, "MyIconButton Scale " & Uuid)
        End If
        RefreshAnim() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub
    Private Sub Button_MouseLeave() Handles Me.MouseLeave
        If Not CanClick Then Return
        IsMouseDown = False
        AniStart({
                     AaScaleTransform(PanBack, 1 - CType(PanBack.RenderTransform, ScaleTransform).ScaleX, 250,, New AniEaseOutFluent)
                 }, "MyIconButton Scale " & Uuid)
        RefreshAnim() '直接刷新颜色以判断是否已触发 MouseLeave
    End Sub

    '务必放在 IsMouseDown 更新之后
    Private Const AnimationColorIn As Integer = 120
    Private Const AnimationColorOut As Integer = 150
    '当前图标颜色（区分填充/描边模式）
    Private Function CurPathBrush() As Brush
        If LogoStroke Then Return Path.Stroke Else Return Path.Fill
    End Function
    Private Sub SetPathBrush(b As Brush)
        If LogoStroke Then
            Path.Fill = Nothing
            Path.Stroke = b
            Path.StrokeThickness = LogoStrokeThickness
        Else
            Path.Stroke = Nothing
            Path.Fill = b
        End If
    End Sub

    Public Sub RefreshAnim() Handles Me.MouseEnter, Me.MouseLeave, Me.Loaded, Me.IsEnabledChanged
        Try
            Dim Prop = If(LogoStroke, Shape.StrokeProperty, Shape.FillProperty)
            If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画
                If PanBack.Background Is Nothing Then PanBack.Background = New MyColor(0, 255, 255, 255)
                If (LogoStroke AndAlso Path.Stroke Is Nothing) OrElse (Not LogoStroke AndAlso Path.Fill Is Nothing) Then
                    Select Case Theme
                        Case Themes.Red
                            SetPathBrush(New MyColor(160, 255, 76, 76))
                        Case Themes.Black
                            SetPathBrush(New MyColor(160, 0, 0, 0))
                        Case Themes.Custom
                            SetPathBrush(New MyColor(160, Foreground))
                        Case Else
                            SetPathBrush(If(Theme = Themes.White, New MyColor(160, 255, 255, 255), New MyColor(160, 128, 128, 128)))
                    End Select
                End If
                Dim AnimList As New List(Of AniData)
                If Not IsEnabled Then
                    AnimList.Add(AaColor(Path, Prop, "ColorBrushGray5", AnimationColorIn))
                ElseIf IsMouseOver Then '指向
                    Select Case Theme
                        Case Themes.Color
                            AnimList.Add(AaColor(Path, Prop, "ColorBrush2", AnimationColorIn))
                        Case Themes.White
                            AnimList.Add(AaColor(PanBack, BackgroundProperty, New MyColor(50, 255, 255, 255) - PanBack.Background, AnimationColorIn))
                        Case Themes.Red
                            AnimList.Add(AaColor(Path, Prop, New MyColor(255, 76, 76) - CurPathBrush(), AnimationColorIn))
                        Case Themes.Black
                            AnimList.Add(AaColor(Path, Prop, New MyColor(230, 0, 0, 0) - CurPathBrush(), AnimationColorIn))
                        Case Themes.Custom
                            AnimList.Add(AaColor(Path, Prop, New MyColor(255, Foreground) - CurPathBrush(), AnimationColorIn))
                    End Select
                Else '普通
                    Select Case Theme
                        Case Themes.Color
                            AnimList.Add(AaColor(Path, Prop, "ColorBrush4", AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                        Case Themes.White
                            AnimList.Add(AaColor(Path, Prop, "ColorBrush8", AnimationColorOut))
                            AnimList.Add(AaColor(PanBack, BackgroundProperty, New MyColor(0, 255, 255, 255) - PanBack.Background, AnimationColorOut))
                        Case Themes.Red
                            AnimList.Add(AaColor(Path, Prop, New MyColor(160, 255, 76, 76) - CurPathBrush(), AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                        Case Themes.Black
                            AnimList.Add(AaColor(Path, Prop, New MyColor(160, 0, 0, 0) - CurPathBrush(), AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                        Case Themes.Custom
                            AnimList.Add(AaColor(Path, Prop, New MyColor(160, Foreground) - CurPathBrush(), AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                    End Select
                End If
                AniStart(AnimList, "MyIconButton Color " & Uuid)
            Else
                AniStop("MyIconButton Color " & Uuid)
                If IsEnabled Then
                    Select Case Theme
                        Case Themes.Color
                            Path.Fill = Nothing : Path.SetResourceReference(Prop, "ColorBrush5")
                        Case Themes.White
                            Path.Fill = Nothing : Path.SetResourceReference(Prop, "ColorBrush8")
                        Case Themes.Red
                            SetPathBrush(New MyColor(160, 255, 76, 76))
                        Case Themes.Black
                            SetPathBrush(New MyColor(160, 0, 0, 0))
                        Case Themes.Custom
                            SetPathBrush(New MyColor(160, Foreground))
                    End Select
                Else
                    Path.Fill = Nothing : Path.SetResourceReference(Prop, "ColorBrushGray5")
                End If
                PanBack.Background = New MyColor(0, 255, 255, 255)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新图标按钮动画状态出错")
        End Try
    End Sub

End Class
Partial Public Module ModAnimation
    Public Sub AniDispose(Control As MyIconButton, RemoveFromChildren As Boolean, Optional CallBack As ParameterizedThreadStart = Nothing)
        If Not Control.IsHitTestVisible Then Return
        Control.IsHitTestVisible = False
        AniStart({
                 AaScaleTransform(Control, -1.5, 200,, New AniEaseInFluent),
                 AaCode(Sub()
                            If RemoveFromChildren Then
                                CType(Control.Parent, Object).Children.Remove(Control)
                            Else
                                Control.Visibility = Visibility.Collapsed
                            End If
                            If CallBack IsNot Nothing Then CallBack(Control)
                        End Sub,, True)
        }, "MyIconButton Dispose " & Control.Uuid)
    End Sub
End Module
