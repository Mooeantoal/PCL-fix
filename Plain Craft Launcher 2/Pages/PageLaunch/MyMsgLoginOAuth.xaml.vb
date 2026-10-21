Public Class MyMsgLoginOAuth
    Private Data As JObject
    Private UserCode As String
    Private DeviceCode As String
    Private Website As String
    Private WebsiteComplete As String
    Private TokenEndpoint As String
    Private ClientId As String

#Region "弹窗"

    Private ReadOnly MyConverter As MyMsgBoxConverter
    Private ReadOnly Uuid As Integer = GetUuid()

    Public Sub New(Converter As MyMsgBoxConverter)
        Try
            InitializeComponent()
            Btn1.Name = Btn1.Name & GetUuid()
            Btn2.Name = Btn2.Name & GetUuid()
            Btn3.Name = Btn3.Name & GetUuid()
            MyConverter = Converter
            ShapeLine.StrokeThickness = GetWPFSize(1)
            Data = Converter.Content
            Init()
        Catch ex As Exception
            Logger.Error(ex, "OAuth 登录弹窗初始化失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub Load(sender As Object, e As EventArgs) Handles MyBase.Loaded
        Try
            Opacity = 0
            AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, If(MyConverter.IsWarn, New MyColor(140, 80, 0, 0), New MyColor(90, 0, 0, 0)) - FrmMain.PanMsg.Background, 200), "PanMsg Background")
            AniStart({
                AaOpacity(Me, 1, 120, 60),
                AaDouble(Sub(i) TransformPos.Y += i, -TransformPos.Y, 300, 60, New AniEaseOutBack(AniEasePower.Weak)),
                AaDouble(Sub(i) TransformRotate.Angle += i, -TransformRotate.Angle, 300, 60, New AniEaseOutFluent(AniEasePower.Weak))
            }, "MyMsgBox " & Uuid)
            Logger.Info($"OAuth 登录弹窗：{LabTitle.Text}{vbCrLf}{LabCaption.Text}")
        Catch ex As Exception
            Logger.Error(ex, "OAuth 登录弹窗加载失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub Close()
        AniStart({
            AaCode(
            Sub()
                If Not WaitingMyMsgBox.Any() Then
                    AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(0, 0, 0, 0) - FrmMain.PanMsg.Background, 200, Ease:=New AniEaseOutFluent(AniEasePower.Weak)))
                End If
            End Sub, 30),
            AaOpacity(Me, -Opacity, 80, 20),
            AaDouble(Sub(i) TransformPos.Y += i, 20 - TransformPos.Y, 150, 0, New AniEaseOutFluent),
            AaDouble(Sub(i) TransformRotate.Angle += i, 6 - TransformRotate.Angle, 150, 0, New AniEaseInFluent(AniEasePower.Weak)),
            AaCode(Sub() CType(Parent, Grid).Children.Remove(Me), , True)
        }, "MyMsgBox " & Uuid)
    End Sub

    Public Sub Btn1_Click() Handles Btn1.Click
    End Sub
    Public Sub Btn3_Click() Handles Btn3.Click
        Finished(New OperationCanceledException)
    End Sub

    Private Sub Drag(sender As Object, e As MouseButtonEventArgs) Handles PanBorder.MouseLeftButtonDown, LabTitle.MouseLeftButtonDown
        On Error Resume Next
        If e.GetPosition(ShapeLine).Y <= 2 Then FrmMain.DragMove()
    End Sub

#End Region

    Private Sub Finished(Result As Object)
        If MyConverter.IsExited Then Return
        MyConverter.IsExited = True
        MyConverter.Result = Result
        RunInUi(AddressOf Close)
        Thread.Sleep(200)
        FrmMain.ShowWindowToTop()
    End Sub

    Private Sub Init()
        UserCode = Data("user_code")
        DeviceCode = Data("device_code")
        Website = Data("verification_uri")
        WebsiteComplete = Data("verification_uri_complete")?.ToString
        TokenEndpoint = Data("token_endpoint")
        ClientId = Data("client_id")
        Dim Interval As Integer = If(Data("interval")?.ToObject(Of Integer), 5)
        Dim ExpiresIn As Integer = If(Data("expires_in")?.ToObject(Of Integer), 600)

        LabTitle.Text = "第三方皮肤站免密登录"
        LabCaption.Text =
            $"登录网页将自动开启，请在网页中输入 {UserCode}（已自动复制）。" & vbCrLf & vbCrLf &
            $"如果网页没有自动打开，你也可以用其他设备打开 {Website} 并输入上述代码。" & vbCrLf &
            $"代码将在 {Math.Floor(ExpiresIn / 60)} 分钟后过期。"

        Dim OpenUrl = If(WebsiteComplete = "", Website, WebsiteComplete)
        CustomEventService.SetEventData(Btn1, OpenUrl)
        CustomEventService.SetEventData(Btn2, UserCode)

        RunInNewThread(AddressOf WorkThread, "MyMsgLoginOAuth")
    End Sub

    Private Sub WorkThread()
        Thread.Sleep(2000)
        If MyConverter.IsExited Then Return
        Dim OpenUrl = If(WebsiteComplete = "", Website, WebsiteComplete)
        OpenWebsite(OpenUrl)
        ClipboardSet(UserCode)
        Dim Interval As Integer = If(Data("interval")?.ToObject(Of Integer), 5)
        Thread.Sleep((Interval - 1) * 1000)

        Dim UnknownFailureCount As Integer = 0
        Do While Not MyConverter.IsExited
            Try
                Dim Result = NetRequestByClient(
                    TokenEndpoint, HttpMethod.Post,
                    Content:=
                        "grant_type=urn:ietf:params:oauth:grant-type:device_code" & "&" &
                        "client_id=" & StringUtils.FormUrlEscape(ClientId) & "&" &
                        "device_code=" & StringUtils.FormUrlEscape(DeviceCode),
                    ContentType:="application/x-www-form-urlencoded",
                    Timeout:=5000 + UnknownFailureCount * 5000,
                    MakeLog:=False, RequireJson:=True)
                Dim ResultJson As JObject = Result.DeserializeJson()
                McLaunchLog($"OAuth 令牌获取成功，过期时间：{ResultJson("expires_in")} 秒")
                Hint("登录成功！", HintType.Green)
                Finished(ResultJson)
                Return
            Catch ex As Exception
                If TypeOf ex Is HttpRequestCodeException Then
                    Dim Response = CType(ex, HttpRequestCodeException).Response
                    If Response.Contains("authorization_declined") Then
                        Finished(New Exception("$你拒绝了 PCL 申请的权限……"))
                        Return
                    ElseIf Response.Contains("expired_token") Then
                        Finished(New Exception("$登录用时太长啦，重新试试吧！"))
                        Return
                    ElseIf Response.Contains("access_denied") Then
                        Finished(New Exception("$你拒绝了 PCL 申请的权限……"))
                        Return
                    ElseIf Response.Contains("authorization_pending") OrElse Response.Contains("slow_down") Then
                        Thread.Sleep(2000)
                        Continue Do
                    End If
                End If
                If UnknownFailureCount <= 2 Then
                    UnknownFailureCount += 1
                    Logger.Warn(ex, $"OAuth 登录轮询第 {UnknownFailureCount} 次失败")
                    Thread.Sleep(2000)
                Else
                    Finished(New Exception("登录轮询失败", ex))
                    Return
                End If
            End Try
        Loop
    End Sub

End Class