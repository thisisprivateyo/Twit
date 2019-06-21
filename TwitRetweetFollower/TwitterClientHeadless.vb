Imports PuppeteerSharp

Public Class TwitterClientHeadless

    Public Property IsLoggedIn As Boolean

    Public Property Email As String
    Public Property Password As String

    Public Enum SessionStatus
        NotAuthenticated
        Authenticated
        IncorrectCredentials
        Locked
        EmailRequired
    End Enum

    Private _sessionStatus As SessionStatus = TwitterClientHeadless.SessionStatus.NotAuthenticated
    Public ReadOnly Property Status As SessionStatus
        Get
            Return _sessionStatus
        End Get
    End Property

    Public Sub Dispose()
        Browser.Dispose()
        Page.Dispose()
    End Sub


    Private Browser As Browser
    Private Session As BrowserContext
    Private Page As Page

    Public Sub New(email As String, password As String)
        Me.Email = email
        Me.Password = password
    End Sub

    Public Async Function TryCreateNew() As Task(Of Boolean)
        Browser = Await Puppeteer.LaunchAsync(New LaunchOptions With {.Headless = False})
        Session = Await Browser.CreateIncognitoBrowserContextAsync
        Return True
    End Function


    Public Async Function TryAuthenticateAsync() As Task(Of Boolean)

        Dim fetcher As New BrowserFetcher

        Dim revision = Await fetcher.DownloadAsync(BrowserFetcher.DefaultRevision)

        Browser = Await Puppeteer.LaunchAsync(New LaunchOptions With {.Headless = True})

        Session = Await Browser.CreateIncognitoBrowserContextAsync

        Page = Await Session.NewPageAsync()

        Await Page.SetJavaScriptEnabledAsync(True)


        Dim response = Await Page.GoToAsync("https://twitter.com/login?lang=en-gb")

        Dim userElement = Await Page.WaitForSelectorAsync("#page-container > div > div.signin-wrapper > form > fieldset > div:nth-child(2) > input")

        If IsNothing(userElement) Then
            Throw New Exception("Could not find valid user input.")
            Return False
        End If

        Await userElement.TypeAsync(Email)

        Dim passElement = Await Page.WaitForSelectorAsync("#page-container > div > div.signin-wrapper > form > fieldset > div:nth-child(3) > input")

        If IsNothing(passElement) Then
            Throw New Exception("Could not find valid pass input.")
            Return False
        End If

        Await passElement.TypeAsync(Password)

        Dim submitElement = Await Page.WaitForSelectorAsync("#page-container > div > div.signin-wrapper > form > div.clearfix > button")

        Await submitElement.TapAsync


        Await Page.WaitForSelectorAsync("#react-root")

        Dim body As String = Await Page.GetContentAsync

        If body.Contains("The email and password that you entered did not match our records.") Then
            _IsLoggedIn = False
            _sessionStatus = SessionStatus.IncorrectCredentials
            Return False
        ElseIf body.Contains("Your account appears to have exhibited unusual behavior that violates the Twitter Rules.") OrElse body.Contains("Are you a robot?") Then
            _IsLoggedIn = False
            _sessionStatus = SessionStatus.Locked

            Return False
        ElseIf body.Contains("Verify your identity by entering the email address associated with your Twitter account.") Then
            _IsLoggedIn = False
            _sessionStatus = SessionStatus.EmailRequired

            Return False
        End If


        _IsLoggedIn = True
        _sessionStatus = SessionStatus.Authenticated

        Return True

    End Function

    Private _tweetList As New List(Of Tweet)
    Public ReadOnly Property TweetList As List(Of Tweet)
        Get
            Return _tweetList
        End Get
    End Property

    Public Class Tweet
        Public Property Id As String
        Public Property RetweeterIds As New List(Of String)
        Public Property DaysStored As Integer
        Public Property DateStored As Date
        Public Function CanDiscard(daysPassed As Integer) As Boolean
            Dim cleanDay As String = (DateStored - Now).Days
            cleanDay = cleanDay.Replace("-", String.Empty)
            Return CInt(cleanDay) >= daysPassed
        End Function
    End Class

    Public Property IgnoredTweetList As New List(Of String)
    Public Property FollowedUserIds As New List(Of String)

    Public Async Function TryCollectTweets() As Task(Of Boolean)

        Dim response = Await Page.GoToAsync($"https://twitter.com/{Email}")

        Await Page.WaitForSelectorAsync("article")

        Dim body As String = Await Page.GetContentAsync

        If Not body.Contains("/status/") Then
            Throw New Exception("No tweets posted yet...")
            Return False
        End If

        Dim tweetIds As String() = Split(body, "/status/")
        Dim outputTweetIds As New List(Of Tweet)

        For i As Integer = 1 To tweetIds.Count - 1
            Dim tweetId As String = Split(tweetIds(i), """")(0)

            If tweetId.Contains("/") Then
                Continue For
            End If

            If IgnoredTweetList.Contains(tweetId) Then
                Continue For
            End If

            If outputTweetIds.Exists(Function(x) x.Id = tweetId) Then
                Continue For
            End If

            Dim tweet As New Tweet With {.Id = tweetId, .DateStored = Now}
            outputTweetIds.Add(tweet)
        Next

        _tweetList = outputTweetIds

        Return True

    End Function


    Public Async Function GetRetweeters(tweet As Tweet) As Task(Of Boolean)

        Dim tweetUrl As String = $"https://twitter.com/{Email}/status/{tweet.Id}/retweets"

        Dim response = Await Page.GoToAsync(tweetUrl)


        Await Page.WaitForSelectorAsync(".css-901oao.css-bfa6kz.r-1re7ezh.r-18u37iz.r-1qd0xha.r-a023e6.r-16dba41.r-ad9z0x.r-bcqeeo.r-qvutc0") '("css-901oao.css-bfa6kz r-1re7ezh r-18u37iz r-1qd0xha r-a023e6 r-16dba41 r-ad9z0x r-bcqeeo r-qvutc0") '(7000)


        Dim body As String = Await Page.GetContentAsync

        If Not body.Contains("<div dir=""ltr"" class=""css-901oao css-bfa6kz r-1re7ezh r-18u37iz r-1qd0xha r-a023e6 r-16dba41 r-ad9z0x r-bcqeeo r-qvutc0""><span dir=""auto"" class=""css-901oao css-16my406 r-1qd0xha r-ad9z0x r-bcqeeo r-qvutc0"">") Then
            Return False
        End If

        Dim retweeters As String() = Split(body, "<div class=""css-1dbjc4n r-18u37iz r-1wbh5a2""><div dir=""ltr"" class=""css-901oao css-bfa6kz r-1re7ezh r-18u37iz r-1qd0xha r-a023e6 r-16dba41 r-ad9z0x r-bcqeeo r-qvutc0""><span dir=""auto"" class=""css-901oao css-16my406 r-1qd0xha r-ad9z0x r-bcqeeo r-qvutc0"">")

        For i As Integer = 1 To retweeters.Count - 1

            Dim user As String = Split(Split(retweeters(i), "@")(1), "</span>")(0)

            If tweet.RetweeterIds.Contains(user) Then

                Continue For
            End If

            If user = Email Then
                Continue For
            End If

            If FollowedUserIds.Contains(user) Then
                Continue For
            End If

            tweet.RetweeterIds.Add(user)

        Next

        Return tweet.RetweeterIds.Count > 0


    End Function

    Public Async Function TryFollowUser(user As String) As Task(Of Boolean)

        Dim userUrl As String = $"https://twitter.com/{user}/"
        Dim body As String = String.Empty

        Dim newPage = Await Session.NewPageAsync

        Dim response = Await newPage.GoToAsync(userUrl)

        Await Page.WaitForSelectorAsync("#react-root")

        body = Await newPage.GetContentAsync

        If body.Contains("-unfollow") Then
            Await newPage.CloseAsync()
            Return False
        End If


        Dim followButtonElement = Await newPage.WaitForSelectorAsync("#react-root > div > div > div.css-1dbjc4n.r-18u37iz.r-1pi2tsx.r-sa2ff0.r-13qz1uu.r-417010 > main > div > div.css-1dbjc4n.r-aqfbo4.r-1niwhzg.r-16y2uox > div > div.css-1dbjc4n.r-14lw9ot.r-1tlfku8.r-1ljd8xs.r-13l2t4g.r-1phboty.r-1jgb5lz.r-1ye8kvj.r-13qz1uu.r-184en5c > div > div > div.css-1dbjc4n.r-6337vo > div > div:nth-child(1) > div > div.css-1dbjc4n.r-obd0qt.r-18u37iz.r-1w6e6rj.r-1wtj0ep > div") '("#page-container > div.ProfileCanopy.ProfileCanopy--withNav.ProfileCanopy--large.js-variableHeightTopBar > div > div.ProfileCanopy-navBar.u-boxShadow > div > div > div.Grid-cell.u-size2of3.u-lg-size3of4 > div > div > ul > li.ProfileNav-item.ProfileNav-item--userActions.u-floatRight.u-textRight.with-rightCaret > div > div > span.user-actions-follow-button.js-follow-btn.follow-button > button.EdgeButton.EdgeButton--secondary.EdgeButton--medium.button-text.follow-text")

        Await followButtonElement.ClickAsync

        body = Await newPage.GetContentAsync

        Await newPage.CloseAsync()

        Return body.Contains("Following</span>")


    End Function
End Class
