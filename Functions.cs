using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Newtonsoft.Json;
using PhotoAlbum.Models.Requests;
using PhotoAlbum.Models.Responses;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using System.IO;
using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;

[assembly:LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace PhotoAlbum
{
 
    public class Functions
    {
       public Response Hello(Request request)
       {
           return new Response("Go Serverless v1.0! Your function executed successfully!", request);
       }


       public async Task<APIGatewayProxyResponse> CreateAlbum(APIGatewayProxyRequest req, ILambdaContext context)
       {
         var logger = context.Logger;

          logger.LogLine($"request {JsonConvert.SerializeObject(req)}");
          logger.LogLine($"context {JsonConvert.SerializeObject(context)}");

          try {

          var request = JsonConvert.DeserializeObject<CreateAlbumRequest>(req.Body);

          if (string.IsNullOrEmpty(request.Name) || (!(request.Year > 0)) || string.IsNullOrEmpty(request.Owner)  )
          {
            return new APIGatewayProxyResponse {
                
              StatusCode = 200,
              Body = JsonConvert.SerializeObject(new CreateAlbumResponse { IsSuccess = false, ErrorMessage = $"Request has some missing parameters. request [JsonConvert.SerializeObject(request)]"})
            } ;
          }

          var albumsTablename = Environment.GetEnvironmentVariable("ALBUMS_TABLE");
          logger.LogLine($"albumsTablename {albumsTablename}");

         var client = new AmazonDynamoDBClient();
          var table = Table.LoadTable(client, albumsTablename);
          var item = new Document();

          var id = Guid.NewGuid();
          var datecreated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff",
                                            CultureInfo.InvariantCulture);

          item["partition_key"] = $"{request.Year}_{datecreated}";
          item["sort_key"] = $"alb";
          item["id"] = id;
          item["year"] = request.Year;
          item["name"] = request.Name;
          item["owner"] = request.Owner;
          item["datecreated"] = datecreated;

          logger.LogLine("Saving doc");
          await table.PutItemAsync(item);
          
          logger.LogLine("Album created");
         return new APIGatewayProxyResponse {
                
              StatusCode = 200,
              Body = JsonConvert.SerializeObject(new CreateAlbumResponse { Id = id, 
              Name = request.Name, Owner = request.Owner,
              Year = request.Year, DateCreated = datecreated, IsSuccess = true })
            } ;
          }
          catch (Exception ee)
          {
            return new APIGatewayProxyResponse {
                
              StatusCode = 500,
              Body = ee.ToString()
            } ;
          }
       }


       public async Task<CreatePhotoResponse> CreatePhoto(CreatePhotoRequest request, ILambdaContext context)
       {
         var logger = context.Logger;

          logger.LogLine($"request {JsonConvert.SerializeObject(request)}");
          logger.LogLine($"context {JsonConvert.SerializeObject(context)}");

          var albumsTablename = Environment.GetEnvironmentVariable("ALBUMS_TABLE");
          logger.LogLine($"albumsTablename {albumsTablename}");

         var client = new AmazonDynamoDBClient();
          var table = Table.LoadTable(client, albumsTablename);
          var item = new Document();

          var id = Guid.NewGuid();
          var uploadDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff",
                                            CultureInfo.InvariantCulture);

          item["partition_key"] = request.AlbumId;
          item["sort_key"] = $"pic_{uploadDate}_{request.Filename}";
          item["id"] = id;
          item["orig_filename"] = request.Filename;
          item["description"] = request.Description;
          item["file_key"] = request.FileKey;
          item["thumbnail_key"] = request.ThumbnailKey;
          item["owner"] = request.Owner;

          item["dateuploaded"] = uploadDate;

          logger.LogLine("Saving doc");
          await table.PutItemAsync(item);
          
          logger.LogLine("Album created");
         return new CreatePhotoResponse { Id = id };
       }

       public async Task CreateThumbnail(S3Event evnt, ILambdaContext context)
       {
          var logger = context.Logger;

          logger.LogLine($"CreateThumbnail3: request {JsonConvert.SerializeObject(evnt)}");
          logger.LogLine($"CreateThumbnail4: context {JsonConvert.SerializeObject(context)}");
         
          var srcBucket = evnt.Records[0].S3.Bucket.Name;
          var dstBucket = srcBucket + "-thumbnails";
          var fileKey = evnt.Records[0].S3.Object.Key;

        var imageType = System.IO.Path.GetExtension(fileKey).Replace(".", "").ToLower();
        logger.LogLine($"Image type: " + imageType);

        if (imageType != "jpg" && imageType != "png") {
            logger.LogLine($"Uploaded file: {evnt.Records[0].S3.Object.Key}.  Unsupported image type: ${imageType}.  No thumbnail created.");
            return;
        }
       //ImageFormat imageFormat = ImageFormat.Png; 
       //i/f (imageType == "jpg")
       //   imageFormat = ImageFormat.Jpeg;

       logger.LogLine($"Getting s3client");
        var s3Client = new AmazonS3Client(Amazon.RegionEndpoint.EUWest2);
       logger.LogLine($"try GetObjectAsync");
        var origimage = await s3Client.GetObjectAsync(srcBucket, fileKey);
        logger.LogLine($"GetObjectAsync: {origimage.BucketName}/{origimage.Key} successful");

        using (Stream responseStream = origimage.ResponseStream)
        {
        using (var outStream = new MemoryStream())
          {
              IImageFormat imgFormat;
              using (var thumbImg = Image.Load(responseStream, out imgFormat))
              {
                  var origSize = thumbImg.Size();

                  if (origSize.Width > 200)
                  {
                    thumbImg.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Crop,
                        Position = AnchorPositionMode.Center,
                        Size = new SixLabors.ImageSharp.Size(200, Convert.ToInt32(origSize.Height / origSize.Width * 200 ))
                    })
                    .AutoOrient());
                  }
                  thumbImg.Save(outStream, imgFormat);

                  Amazon.S3.Model.PutObjectRequest putRequest = new Amazon.S3.Model.PutObjectRequest( );

                  putRequest.BucketName = dstBucket;
                  putRequest.ContentType = "image/" + imageType;
                  putRequest.Key = evnt.Records[0].S3.Object.Key;
                  putRequest.InputStream = outStream;

                  await s3Client.PutObjectAsync(putRequest);
              }
          }
        }

      logger.LogLine($"Successfully resized {srcBucket}/{fileKey} and uploaded to {dstBucket}/{fileKey}");
    }

 
    }

    public class LambdaRequest
    {

      public string body { get; set; }

    }
    public class Response
    {
      public string Message {get; set;}
      public Request Request {get; set;}

      public Response(string message, Request request){
        Message = message;
        Request = request;
      }
    }

    public class Request
    {
      public string Key1 {get; set;}
      public string Key2 {get; set;}
      public string Key3 {get; set;}

      public Request(string key1, string key2, string key3){
        Key1 = key1;
        Key2 = key2;
        Key3 = key3;
      }
    }

    
}
