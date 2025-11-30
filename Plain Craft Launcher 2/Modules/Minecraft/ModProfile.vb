Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.IO
Imports PCL.Core.App

Public Module ModProfile

    ''' <summary>
    ''' 档案类
    ''' </summary>
    Public Class McProfile
        Public Property Type As McLoginType
        Public Property Uuid As String
        Public Property Username As String
        Public Property Desc As String
        Public Property AccessToken As String
        Public Property RefreshToken As String
        Public Property ClientToken As String
        Public Property Server As String
        Public Property ServerName As String
        Public Property Name As String
        Public Property Password As String
        Public Property Expires As Long
        Public Property SkinHeadId As String
    End Class

    ''' <summary>
    ''' 当前选定的档案
    ''' </summary>
    Public SelectedProfile As McProfile = Nothing
    ''' <summary>
    ''' 上次选定的档案编号
    ''' </summary>
    Public LastUsedProfile As Integer = Nothing
    ''' <summary>
    ''' 档案列表
    ''' </summary>
    Public ProfileList As New List(Of McProfile)
    Public IsCreatingProfile As Boolean = False
    ''' <summary>
    ''' 档案操作日志
    ''' </summary>
    Public Sub ProfileLog(content As String, Optional level As LogLevel = LogLevel.Normal)
        Log("[Profile] " & content, level)
    End Sub

#Region "旧版迁移"
    ''' <summary>
    ''' 从旧版配置文件迁移档案，不能在 UI 线程调用
    ''' </summary>
    Public Sub MigrateOldProfile()
        ProfileLog("开始从旧版配置迁移档案")
        Dim profileCount As Integer = 0
        '离线档案
        If Not String.IsNullOrWhiteSpace(Setup.Get("LoginLegacyName")) Then
            Dim oldOfflineInfo As String() = Setup.Get("LoginLegacyName").Split("¨")
            ProfileLog($"找到 {oldOfflineInfo.Count} 个旧版离线档案信息")
            For Each OfflineId In oldOfflineInfo
                Dim newProfile As New McProfile With {.Username = OfflineId, .Uuid = GetOfflineUuid(OfflineId, IsLegacy:=True), .Type = McLoginType.Legacy} '迁移的档案默认使用旧版 UUID 生成方式以避免存档丢失
                ProfileList.Add(newProfile)
                profileCount += 1
            Next
            SaveProfile()
            ProfileLog("旧版离线档案迁移完成")
            Setup.Reset("LoginLegacyName")
        Else
            ProfileLog("无旧版离线档案信息")
        End If
        '第三方验证档案
        If Not (String.IsNullOrWhiteSpace(Setup.Get("CacheAuthName")) OrElse String.IsNullOrWhiteSpace(Setup.Get("CacheAuthUuid")) OrElse String.IsNullOrWhiteSpace(Setup.Get("CacheAuthServerServer")) OrElse String.IsNullOrWhiteSpace(Setup.Get("CacheAuthUsername")) OrElse String.IsNullOrWhiteSpace(Setup.Get("CacheAuthPass"))) Then
            ProfileLog($"找到旧版第三方验证档案信息")
            Dim newProfile As New McProfile With {.Username = Setup.Get("CacheAuthName"), .Uuid = Setup.Get("CacheAuthUuid"),
                    .Name = Setup.Get("CacheAuthUsername"), .Password = Setup.Get("CacheAuthPass"), .Server = Setup.Get("CacheAuthServerServer") & "/authserver", .Type = McLoginType.Auth}
            ProfileList.Add(newProfile)
            SaveProfile()
            ProfileLog("旧版第三方验证档案迁移完成")
            profileCount += 1
            Setup.Reset("CacheAuthName")
            Setup.Reset("CacheAuthUuid")
            Setup.Reset("CacheAuthServerServer")
            Setup.Reset("CacheAuthUsername")
            Setup.Reset("CacheAuthPass")
        Else
            ProfileLog("无旧版第三方验证档案信息")
        End If
        If Not profileCount = 0 Then Hint($"已自动从旧版配置文件迁移档案，共迁移了 {profileCount} 个档案")
        ProfileLog("档案迁移结束")
    End Sub
#End Region

#Region "档案操作"
    ''' <summary>
    ''' 获取档案列表
    ''' </summary>
    Public Sub GetProfile()
        '从配置文件读取档案列表
        Dim ProfileJson As String = Setup.Get("LoginProfile")
        If Not String.IsNullOrWhiteSpace(ProfileJson) Then
            Try
                Dim ProfileArray As JArray = JArray.Parse(ProfileJson)
                ProfileList.Clear()
                For Each Profile In ProfileArray
                    Dim NewProfile As New McProfile With {
                        .Type = CType(Profile("Type").ToObject(Of Integer)(), McLoginType),
                        .Uuid = Profile("Uuid"),
                        .Username = Profile("Username"),
                        .Desc = Profile("Desc"),
                        .AccessToken = Profile("AccessToken"),
                        .RefreshToken = Profile("RefreshToken"),
                        .ClientToken = Profile("ClientToken"),
                        .Server = Profile("Server"),
                        .ServerName = Profile("ServerName"),
                        .Name = Profile("Name"),
                        .Password = Profile("Password"),
                        .Expires = Profile("Expires").ToObject(Of Long)()
                    }
                    ProfileList.Add(NewProfile)
                Next
            Catch ex As Exception
                Log(ex, "读取档案列表失败", LogLevel.Feedback)
                ProfileList.Clear()
            End Try
        End If
        '获取上次使用的档案编号
        Dim temp As Object = Setup.Get("LoginProfileLast")
        LastUsedProfile = If(temp Is Nothing, 0, CInt(temp))
    End Sub

    ''' <summary>
    ''' 保存档案列表
    ''' </summary>
    Public Sub SaveProfile()
        '保存档案列表
        Dim ProfileArray As New JArray
        For Each Profile In ProfileList
            Dim ProfileJson As New JObject From {
                {"Type", Profile.Type},
                {"Uuid", Profile.Uuid},
                {"Username", Profile.Username},
                {"Desc", Profile.Desc},
                {"AccessToken", Profile.AccessToken},
                {"RefreshToken", Profile.RefreshToken},
                {"ClientToken", Profile.ClientToken},
                {"Server", Profile.Server},
                {"ServerName", Profile.ServerName},
                {"Name", Profile.Name},
                {"Password", Profile.Password},
                {"Expires", Profile.Expires}
            }
            ProfileArray.Add(ProfileJson)
        Next
        Setup.Set("LoginProfile", ProfileArray.ToString)
        '保存上次使用的档案编号
        If SelectedProfile IsNot Nothing Then
            LastUsedProfile = ProfileList.IndexOf(SelectedProfile)
            Setup.Set("LoginProfileLast", LastUsedProfile)
        End If
    End Sub

    ''' <summary>
    ''' 获取离线UUID
    ''' </summary>
    ''' <param name="name">玩家名称</param>
    ''' <param name="IsLegacy">是否使用旧版UUID生成方式</param>
    Public Function GetOfflineUuid(name As String, Optional IsLegacy As Boolean = False) As String
        If String.IsNullOrWhiteSpace(name) Then Return StrFill("", "0", 32)
        '旧版UUID生成方式
        If IsLegacy Then
            Return StrFill("", "0", 32)
        End If
        '新版UUID生成方式
        Using Md5 As MD5 = MD5.Create()
            Dim HashBytes As Byte() = Md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" & name))
            Return BitConverter.ToString(HashBytes).Replace("-", "").ToLower
        End Using
    End Function
    
    ''' <summary>
    ''' 获取档案信息描述
    ''' </summary>
    ''' <param name="profile">档案对象</param>
    ''' <returns>档案信息描述</returns>
    Public Function GetProfileInfo(profile As McProfile) As String
        Select Case profile.Type
            Case McLoginType.Legacy
                Return "离线模式"
            Case McLoginType.Auth
                Return "第三方验证"
            Case Else
                Return "未知类型"
        End Select
    End Function
    
    ''' <summary>
    ''' 创建档案
    ''' </summary>
    Public Sub CreateProfile()
        IsCreatingProfile = True
        Try
            Dim RadioBoxes As New List(Of IMyRadio)
            RadioBoxes.Add(New MyRadioBox With {.Text = "离线模式"})
            RadioBoxes.Add(New MyRadioBox With {.Text = "第三方验证"})
            Dim ProfileType As Integer = MyMsgBoxSelect(RadioBoxes, "选择档案类型")
            If ProfileType = -1 Then Exit Sub
            
            Select Case ProfileType
                Case 0 '离线模式
                    Dim Username As String = MyMsgBoxInput("创建离线档案", "请输入用户名")
                    If Username Is Nothing Then Exit Sub
                    If String.IsNullOrWhiteSpace(Username) Then
                        Hint("用户名不能为空！", HintType.Critical)
                        Exit Sub
                    End If
                    
                    Dim NewProfile As New McProfile With {
                        .Type = McLoginType.Legacy,
                        .Username = Username,
                        .Uuid = GetOfflineUuid(Username)
                    }
                    ProfileList.Add(NewProfile)
                    SaveProfile()
                    ProfileLog("创建离线档案成功")
                    
                Case 1 '第三方验证
                    Dim Server As String = MyMsgBoxInput("创建第三方验证档案", "请输入验证服务器地址", "https://authserver.example.org")
                    If Server Is Nothing Then Exit Sub
                    If String.IsNullOrWhiteSpace(Server) Then
                        Hint("服务器地址不能为空！", HintType.Critical)
                        Exit Sub
                    End If
                    
                    Dim Name As String = MyMsgBoxInput("创建第三方验证档案", "请输入用户名")
                    If Name Is Nothing Then Exit Sub
                    If String.IsNullOrWhiteSpace(Name) Then
                        Hint("用户名不能为空！", HintType.Critical)
                        Exit Sub
                    End If
                    
                    Dim Password As String = MyMsgBoxInput("创建第三方验证档案", "请输入密码", "")
                    If Password Is Nothing Then Exit Sub
                    If String.IsNullOrWhiteSpace(Password) Then
                        Hint("密码不能为空！", HintType.Critical)
                        Exit Sub
                    End If
                    
                    Dim NewProfile As New McProfile With {
                        .Type = McLoginType.Auth,
                        .Name = Name,
                        .Password = Password,
                        .Server = Server
                    }
                    ProfileList.Add(NewProfile)
                    SaveProfile()
                    ProfileLog("创建第三方验证档案成功")
            End Select
        Catch ex As Exception
            Log(ex, "创建档案失败", LogLevel.Feedback)
        Finally
            IsCreatingProfile = False
        End Try
    End Sub
    
    ''' <summary>
    ''' 编辑离线UUID
    ''' </summary>
    ''' <param name="profile">档案对象</param>
    Public Sub EditOfflineUuid(profile As McProfile)
        If profile.Type <> McLoginType.Legacy Then Exit Sub
        
        Dim Uuid As String = MyMsgBoxInput("修改 UUID", "请输入新的 UUID", profile.Uuid)
        If Uuid IsNot Nothing Then
            profile.Uuid = Uuid
            SaveProfile()
            ProfileLog("修改离线UUID成功")
        End If
    End Sub
    
    ''' <summary>
    ''' 编辑验证服务器名称
    ''' </summary>
    ''' <param name="profile">档案对象</param>
    ''' <param name="name">新的服务器名称</param>
    Public Sub EditAuthServerName(profile As McProfile, name As String)
        If profile.Type <> McLoginType.Auth Then Exit Sub
        
        profile.ServerName = name
        SaveProfile()
        ProfileLog("修改验证服务器名称成功")
    End Sub
    
    ''' <summary>
    ''' 删除档案
    ''' </summary>
    ''' <param name="profile">档案对象</param>
    Public Sub RemoveProfile(profile As McProfile)
        ProfileList.Remove(profile)
        If SelectedProfile Is profile Then SelectedProfile = Nothing
        SaveProfile()
        ProfileLog("删除档案成功")
    End Sub
    
    ''' <summary>
    ''' 迁移档案（导入/导出）
    ''' </summary>
    Public Sub MigrateProfile()
        Try
            ProfileLog("开始迁移档案")
            Hint("档案迁移功能已移除", HintType.Info)
        Catch ex As Exception
            Log(ex, "迁移档案失败", LogLevel.Feedback)
        End Try
    End Sub
    
    ''' <summary>
    ''' 编辑档案ID
    ''' </summary>
    ''' <param name="profile">档案对象</param>
    Public Sub EditProfileId(profile As McProfile)
        Try
            Dim NewId As String = MyMsgBoxInput("修改档案ID", "请输入新的档案ID", profile.Uuid)
            If NewId IsNot Nothing Then
                profile.Uuid = NewId
                SaveProfile()
                ProfileLog("修改档案ID成功")
            End If
        Catch ex As Exception
            Log(ex, "修改档案ID失败", LogLevel.Feedback)
        End Try
    End Sub
#End Region

End Module



