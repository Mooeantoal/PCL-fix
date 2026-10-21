Public Class PageLoginAuth
    Private IsFirstLoad As Boolean = True
    Private IsOAuthSupported As Boolean = False

    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        '检测是否支持 OAuth
        RunInNewThread(AddressOf CheckOAuthSupport, "PageLoginAuth CheckOAuth")

        '记住密码
        CheckRemember.Checked = Settings.Get(Of Boolean)("LoginRemember")
        If KeepInput AndAlso Not IsFirstLoad Then
            Dim Input As String = ComboName.Text
            ComboName.ItemsSource = If(Settings.Get(Of String)("LoginAuthEmail") = "", Nothing, Settings.Get(Of String)("LoginAuthEmail").Split("¨"))
            ComboName.Text = Input
        Else
            If Settings.Get(Of String)("LoginAuthEmail") = "" Then
                ComboName.ItemsSource = Nothing
            Else
                ComboName.ItemsSource = Settings.Get(Of String)("LoginAuthEmail").Split("¨")
                ComboName.Text = Settings.Get(Of String)("LoginAuthEmail").BeforeFirst("¨")
                If Settings.Get(Of Boolean)("LoginRemember") Then TextPass.Password = Settings.Get(Of String)("LoginAuthPass").BeforeFirst("¨").Trim
            End If
        End If
        IsFirstLoad = False

        '恢复 OAuth 开关状态
        CheckOAuth.Checked = (Settings.Get(Of McLoginType)("LoginType") = McLoginType.AuthOAuth)
        UpdateOAuthMode()
    End Sub

    ''' <summary>
    ''' 检测当前皮肤站是否支持 Yggdrasil Connect。
    ''' </summary>
    Private Sub CheckOAuthSupport()
        Try
            Dim Server As String = If(McInstanceSelected Is Nothing,
                Settings.Get(Of String)("CacheAuthServerServer"),
                Settings.Get(Of String)("VersionServerAuthServer", Instance:=McInstanceSelected))
            If Server.IsEmpty Then
                RunInUi(Sub() CheckOAuth.Visibility = Visibility.Collapsed)
                Return
            End If
            Dim Config = McGetOAuthConfig(Server)
            IsOAuthSupported = (Config IsNot Nothing)
            RunInUi(Sub()
                        If IsOAuthSupported Then
                            CheckOAuth.Visibility = Visibility.Visible
                        Else
                            CheckOAuth.Visibility = Visibility.Collapsed
                            CheckOAuth.Checked = False
                            UpdateOAuthMode()
                        End If
                    End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "检测 OAuth 支持失败")
            RunInUi(Sub() CheckOAuth.Visibility = Visibility.Collapsed)
        End Try
    End Sub

    ''' <summary>
    ''' 根据 OAuth 开关切换 UI 显示。
    ''' </summary>
    Private Sub UpdateOAuthMode()
        Dim UseOAuth = CheckOAuth.Checked
        If UseOAuth Then
            ComboName.Visibility = Visibility.Collapsed
            LabName.Visibility = Visibility.Collapsed
            TextPass.Visibility = Visibility.Collapsed
            LabPass.Visibility = Visibility.Collapsed
            CheckRemember.Visibility = Visibility.Collapsed
            BtnLink.Visibility = Visibility.Collapsed
            '判断是否已登录
            Dim CachedName = Settings.Get(Of String)("CacheOAuthName")
            If Not CachedName.IsEmpty Then
                LabOAuthName.Text = CachedName
                PanOAuth.Visibility = Visibility.Collapsed
                PanOAuthLoggedIn.Visibility = Visibility.Visible
            Else
                PanOAuth.Visibility = Visibility.Visible
                PanOAuthLoggedIn.Visibility = Visibility.Collapsed
            End If
        Else
            ComboName.Visibility = Visibility.Visible
            LabName.Visibility = Visibility.Visible
            TextPass.Visibility = Visibility.Visible
            LabPass.Visibility = Visibility.Visible
            CheckRemember.Visibility = Visibility.Visible
            ReloadRegisterButton()
            PanOAuth.Visibility = Visibility.Collapsed
            PanOAuthLoggedIn.Visibility = Visibility.Collapsed
        End If
    End Sub

    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginData
        Dim Server As String = If(IsNothing(McInstanceSelected), Settings.Get(Of String)("CacheAuthServerServer"), Settings.Get(Of String)("VersionServerAuthServer", Instance:=McInstanceSelected)) & "/authserver"
        If FrmLoginAuth Is Nothing Then
            Return New McLoginServer(McLoginType.Auth) With {.Token = "Auth", .BaseUrl = Server, .UserName = "", .Password = "", .Description = "Authlib-Injector", .Type = McLoginType.Auth}
        Else
            If FrmLoginAuth.CheckOAuth.Checked Then
                Return New McLoginAuth With {.Server = Server, .Type = McLoginType.AuthOAuth}
            Else
                Return New McLoginServer(McLoginType.Auth) With {.Token = "Auth", .BaseUrl = Server, .UserName = FrmLoginAuth.ComboName.Text.Replace("¨", "").Trim, .Password = FrmLoginAuth.TextPass.Password.Replace("¨", "").Trim, .Description = "Authlib-Injector", .Type = McLoginType.Auth}
            End If
        End If
    End Function
    ''' <summary>
    ''' 当前页面的登录信息是否有效。
    ''' </summary>
    Public Shared Function IsVaild(LoginData As McLoginData) As String
        If LoginData.Type = McLoginType.AuthOAuth Then Return ""
        Dim ServerData = TryCast(LoginData, McLoginServer)
        If ServerData Is Nothing Then Return "登录数据类型错误"
        If ServerData.UserName = "" Then Return "账号不能为空！"
        If ServerData.Password = "" Then Return "密码不能为空！"
        Return ""
    End Function
    Public Function IsVaild() As String
        Return IsVaild(GetLoginData())
    End Function

    '保存输入信息
    Private Sub ComboName_TextChanged(sender As Object, e As TextChangedEventArgs) Handles ComboName.TextChanged
        If sender.Text = "" Then TextPass.Password = ""
        If AniControlEnabled = 0 Then Settings.Set("CacheAuthAccess", "")
        BtnLink.Content = If(ComboName.Text = "", "注册账号", "找回密码")
    End Sub
    Private Sub TextPass_PasswordChanged(sender As Object, e As RoutedEventArgs) Handles TextPass.PasswordChanged
        If AniControlEnabled = 0 Then Settings.Set("CacheAuthAccess", "")
    End Sub
    Private Sub ComboName_SelectionChanged(sender As MyComboBox, e As SelectionChangedEventArgs) Handles ComboName.SelectionChanged
        If sender.SelectedIndex = -1 OrElse Not Settings.Get(Of Boolean)("LoginRemember") Then
            TextPass.Password = ""
        Else
            TextPass.Password = Settings.Get(Of String)("LoginAuthPass").ToString.Split("¨")(sender.SelectedIndex).Trim
        End If
    End Sub
    Private Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckRemember.Change
        If AniControlEnabled = 0 Then Settings.Set(sender.Tag, sender.Checked)
    End Sub

    'OAuth 开关
    Private Sub CheckOAuth_Change(sender As MyCheckBox, e As Object) Handles CheckOAuth.Change
        If AniControlEnabled <> 0 Then Return
        UpdateOAuthMode()
        If sender.Checked Then
            Settings.Set("LoginType", McLoginType.AuthOAuth)
        Else
            Settings.Set("LoginType", McLoginType.Auth)
        End If
    End Sub

    'OAuth 登录按钮
    Private Sub BtnOAuthLogin_Click(sender As Object, e As EventArgs) Handles BtnOAuthLogin.Click
        '直接触发启动流程，McLoginLoader 会调用 GetLoginData 获取 AuthOAuth 类型数据
        FrmLaunchLeft.LaunchButtonClick()
    End Sub

    'OAuth 退出登录
    Private Sub BtnOAuthLogout_Click(sender As Object, e As EventArgs) Handles BtnOAuthLogout.Click
        Settings.Set("CacheOAuthAccess", "")
        Settings.Set("CacheOAuthClient", "")
        Settings.Set("CacheOAuthUuid", "")
        Settings.Set("CacheOAuthName", "")
        Settings.Set("CacheOAuthRefresh", "")
        Settings.Set("CacheOAuthIdToken", "")
        McLoginAuthOAuthLoader.Input = Nothing
        UpdateOAuthMode()
        Hint("已退出登录", HintType.Green)
    End Sub

    '链接处理
    Private Sub BtnLink_Click(sender As Object, e As EventArgs) Handles BtnLink.Click
        If BtnLink.Content = "注册账号" Then
            OpenWebsite(If(McInstanceSelected IsNot Nothing, Settings.Get(Of String)("VersionServerAuthRegister", Instance:=McInstanceSelected), Settings.Get(Of String)("CacheAuthServerRegister")))
        Else
            Dim Website As String = If(McInstanceSelected IsNot Nothing, Settings.Get(Of String)("VersionServerAuthRegister", Instance:=McInstanceSelected), Settings.Get(Of String)("CacheAuthServerRegister"))
            OpenWebsite(Website.Replace("/auth/register", "/auth/forgot"))
        End If
    End Sub
    '切换注册按钮可见性
    Private Sub ReloadRegisterButton()
        Dim Address As String = If(McInstanceSelected IsNot Nothing, Settings.Get(Of String)("VersionServerAuthRegister", Instance:=McInstanceSelected), Settings.Get(Of String)("CacheAuthServerRegister"))
        BtnLink.Visibility = If(String.IsNullOrEmpty(New ValidateHttp().Validate(Address)), Visibility.Visible, Visibility.Collapsed)
    End Sub

End Class