CREATE TABLE [dbo].[RewriteTable](
  [OriginalUrl] [nvarchar](256) NOT NULL,
  [NewUrl] [nvarchar](256) NOT NULL
) ON [PRIMARY]
GO

CREATE PROCEDURE [dbo].[GetRewrittenUrls] 
AS
  SELECT OriginalUrl, NewUrl 
  FROM dbo.RewriteTable
GO