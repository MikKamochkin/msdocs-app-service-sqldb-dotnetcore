SELECT TOP (1000) [Id]
      ,[Email]
      ,[MeetingPassword]
      ,[IsBusy]
      ,[ScheduleId]
      ,[ZoomId]
      ,[JoinUrl]
      ,[MeetingId]
      ,[Duration]
      ,[StartTime]
      ,[UUid]
      ,[FirstMutualPresenceTime]
  FROM [DotNetCoreSqlDb].[dbo].[ZoomMeetings]

  insert into dbo.ZoomMeetings (Id, Email, MeetingPassword, IsBusy, ScheduleId, ZoomId, JoinUrl, MeetingId, Duration, StartTime, UUid, FirstMutualPresenceTime)
    values (NEWID(), 'torontofrench02@gmail.com', NULL, 0, NULL, 'yJ0z8XN7QRG0Cl_b-ZSiRA', NULL, NULL, NULL, NULL, NULL, NULL)
