Imports TwitRetweetFollower.TwitterClientHeadless

Module Module1

    Private Client As TwitterClientHeadless


    Private Delay As Integer = 1
    Private Property DiscardDays As Integer = 5

    Sub Main()

        Console.Title = "Twitter Retweet Follower"

        Console.WriteLine("Enter your e-mail address...")

        Dim email As String = Console.ReadLine

        Console.WriteLine("Enter your password...")

        Dim passWord As String = Console.ReadLine

        Console.WriteLine($"Enter amount of days to discard tweets after. Default: 5")

        Dim discard As String = Console.ReadLine

        If Not String.IsNullOrEmpty(discard) Then
            DiscardDays = discard
        End If

        Console.WriteLine($"Enter amount of seconds to delay between follows. Default: 5")

        Dim delayStr = Console.ReadLine

        If Not String.IsNullOrEmpty(delayStr) Then
            Delay = delayStr
        End If

        Client = New TwitterClientHeadless(email, passWord)

        HandleIt()

        Console.ReadKey()

    End Sub

    Private Async Function Login() As Task(Of Boolean)

        Console.WriteLine("Attempting to authenticate...")

        Await Client.TryAuthenticateAsync()

        Select Case Client.Status
            Case TwitterClientHeadless.SessionStatus.IncorrectCredentials
                Console.WriteLine("Could not authenticate. Invalid credentials.")
            Case TwitterClientHeadless.SessionStatus.Authenticated
                Console.WriteLine("Successfully authenticated.")
                Return True
            Case TwitterClientHeadless.SessionStatus.Locked
                Console.WriteLine("Your account is locked and requires captcha/phone verification.")
        End Select

        Return False

    End Function

    Private Sub WriteTweetStatus(tweetId As String, status As String)
        Console.WriteLine($"[Tweet ID = {tweetId}] {status}")
    End Sub

    Private Async Sub HandleIt()

        If Not Await Login() Then
            Console.ReadLine()
            Console.WriteLine("Unable to run.")
            Console.ReadLine()

            Return
        End If


        While True

            Console.WriteLine("Checking for new tweets...")

            Dim hasTweets = Await CollectTweets()

            If Not hasTweets Then
                Await Task.Delay(TimeSpan.FromMinutes(1))
            End If

            For Each tweet In Client.TweetList

                If tweet.CanDiscard(DiscardDays) Then
                    Client.IgnoredTweetList.Add(tweet.Id)
                    Client.TweetList.Remove(tweet)
                    WriteTweetStatus(tweet.Id, $"Tweet is older than {DiscardDays} old and has now been discarded.")
                    Continue For
                End If

                WriteTweetStatus(tweet.Id, "Checking for new retweeters...")

                Dim hasUsers = Await CollectRetweets(tweet)

                If Not hasUsers Then
                    Continue While
                End If

                For Each user In tweet.RetweeterIds
                    Await FollowUser(tweet, user)
                    Await Task.Delay(TimeSpan.FromSeconds(Delay))
                Next

            Next

            Await Task.Delay(TimeSpan.FromMinutes(1))

        End While

    End Sub

    Private Async Function CollectRetweets(tweet As Tweet) As Task(Of Boolean)
        Dim hasRetweeters = Await Client.GetRetweeters(tweet)
        If Not hasRetweeters Then
            WriteTweetStatus(tweet.Id, "Tweet has no retweeters yet.")
        End If
        Return hasRetweeters
    End Function

    Private Async Function CollectTweets() As Task(Of Boolean)
        Try
            Dim hasTweets = Await Client.TryCollectTweets

            If Not hasTweets Then
                Console.WriteLine("No tweets available yet.")
                Return False
            End If

            Return True
        Catch ex As Exception
            Console.WriteLine($"Could not collect tweets > Ex: {ex.Message}")
        End Try
        Return False
    End Function

    Private Sub WriteFollowStatus(tweet As Tweet, user As String, status As String)
        WriteTweetStatus(tweet.Id, $"{status} > {user}")
    End Sub

    Private Async Function FollowUser(tweet As Tweet, userId As String) As Task

        Try

            WriteFollowStatus(tweet, userId, "Attempting to follow...")

            Dim isSuccess As Boolean = Await Client.TryFollowUser(userId)

            If isSuccess Then
                WriteFollowStatus(tweet, userId, "Successfully followed!")
            Else
                WriteFollowStatus(tweet, userId, "Could not follow.")
            End If

        Catch ex As Exception

            Console.WriteLine(ex.Message)
        End Try




    End Function

    Private Async Sub RunFollowProcess()



    End Sub

End Module
