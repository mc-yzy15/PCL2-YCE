Public Class FormProfileCreate
    Inherits Window
    
    ''' <summary>
    ''' 档案创建结果
    ''' </summary>
    Public Property CreateResult As Boolean = False
    
    ''' <summary>
    ''' 新创建的档案对象
    ''' </summary>
    Public Property NewProfile As McProfile = Nothing
    
    Public Sub New()
        InitializeComponent()
        
        ' 初始化服务器下拉列表
        ComboServer.Items.Add("https://authserver.example.org")
        ComboServer.Items.Add("https://littleskin.cn/api/yggdrasil")
        ComboServer.Items.Add("https://auth2.littleskin.cn/api/yggdrasil")
        ComboServer.SelectedIndex = 0
        
        ' 类型选择事件
        AddHandler RadioOffline.Checked, AddressOf ProfileTypeChanged
        AddHandler RadioAuth.Checked, AddressOf ProfileTypeChanged
        
        ' 初始状态
        ProfileTypeChanged(Nothing, Nothing)
    End Sub
    
    ''' <summary>
    ''' 档案类型改变事件
    ''' </summary>
    Private Sub ProfileTypeChanged(sender As Object, e As RoutedEventArgs)
        If RadioOffline.IsChecked Then
            ' 离线模式：禁用服务器和密码选项
            ComboServer.IsEnabled = False
            TextPassword.IsEnabled = False
        Else
            ' 第三方登录：启用服务器和密码选项
            ComboServer.IsEnabled = True
            TextPassword.IsEnabled = True
        End If
    End Sub
    
    ''' <summary>
    ''' 关闭按钮点击事件
    ''' </summary>
    Private Sub BtnClose_Click(sender As Object, e As RoutedEventArgs)
        CreateResult = False
        Close()
    End Sub
    
    ''' <summary>
    ''' 取消按钮点击事件
    ''' </summary>
    Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
        CreateResult = False
        Close()
    End Sub
    
    ''' <summary>
    ''' 确定按钮点击事件
    ''' </summary>
    Private Sub BtnOK_Click(sender As Object, e As RoutedEventArgs)
        Try
            ' 验证输入
            If String.IsNullOrWhiteSpace(TextUsername.Text) Then
                Hint("用户名不能为空！", HintType.Critical)
                Return
            End If
            
            If RadioAuth.IsChecked Then
                ' 第三方登录验证
                If String.IsNullOrWhiteSpace(ComboServer.Text) Then
                    Hint("服务器地址不能为空！", HintType.Critical)
                    Return
                End If
                
                If String.IsNullOrWhiteSpace(TextPassword.Password) Then
                    Hint("密码不能为空！", HintType.Critical)
                    Return
                End If
            End If
            
            ' 创建档案
            If RadioOffline.IsChecked Then
                ' 离线档案
                NewProfile = New McProfile With {
                    .Type = McLoginType.Legacy,
                    .Username = TextUsername.Text,
                    .Uuid = GetOfflineUuid(TextUsername.Text),
                    .Desc = TextDescription.Text
                }
            Else
                ' 第三方登录档案
                NewProfile = New McProfile With {
                    .Type = McLoginType.Auth,
                    .Username = TextUsername.Text,
                    .Name = TextUsername.Text,
                    .Password = TextPassword.Password,
                    .Server = ComboServer.Text,
                    .Desc = TextDescription.Text
                }
            End If
            
            CreateResult = True
            Close()
        Catch ex As Exception
            Log(ex, "创建档案失败", LogLevel.Feedback)
            Hint("创建档案失败：" & ex.Message, HintType.Critical)
        End Try
    End Sub
End Class