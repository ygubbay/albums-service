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
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;

[assembly:LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace PhotoAlbum
{
 
    public class Functions
    {
       public Response Hello(Request request)
       {
           return new Response("Go Serverless v1.0! Your function executed successfully!", request);
       }


       public async Task<CreateAlbumResponse> CreateAlbum(CreateAlbumRequest request)
       {
         var client = new AmazonDynamoDBClient();
          var table = Table.LoadTable(client, "AlbumsTable");
          var item = new Document();

          var id = Guid.NewGuid();
          item["id"] = id;
          item["year"] = request.Year;
          item["name"] = request.Name;
          item["owner"] = request.Owner;
          item["datecreated"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff",
                                            CultureInfo.InvariantCulture);

          await table.PutItemAsync(item);
         return new CreateAlbumResponse { Id = id };
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
