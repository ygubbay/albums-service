using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Newtonsoft.Json;
using PhotoAlbum.Models.Requests;
using PhotoAlbum.Models.Responses;
using System;
using System.Threading.Tasks;
using Amazon.S3;
using System.IO;
using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using System.Collections.Generic;
using System.Linq;
using Amazon.Runtime;
using Amazon.DynamoDBv2.Model;
using System.Web;

[assembly:LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace PhotoAlbum
{
 
    public class Functions
    {
       public Response Hello(Request request)
       {
           return new Response("Go Serverless v1.0! Your function executed successfully!", request);
       }


      public async Task<APIGatewayProxyResponse> GetAlbums(APIGatewayProxyRequest request, ILambdaContext context)
      {
        var logger = context.Logger;

          logger.LogLine($"request {JsonConvert.SerializeObject(request)}");
          logger.LogLine($"context {JsonConvert.SerializeObject(context)}");

          var albumsTablename = Environment.GetEnvironmentVariable("ALBUMS_TABLE");
          
          logger.LogLine($"albumsTablename {albumsTablename}");

          
          var client = new AmazonDynamoDBClient();
          var table = Table.LoadTable(client, albumsTablename);

          var db_request = new QueryRequest
          {
              TableName = albumsTablename,
              IndexName = "gsiAlbums",
              KeyConditionExpression = "sort_key = :v_Id",
              ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                  {":v_Id", new AttributeValue { S = "alb" }}}
          };

          List<Album> albumList = new List<Album>();
          var result = await client.QueryAsync(db_request);

          result.Items.ForEach(item => {

              var album = new Album { Name = item["name"].S, 
                                Owner = item["owner"].S,
                                Year = Convert.ToInt32(item["year"].N),
                                Id = new Guid(item["id"].S),
                                DateCreated = item["datecreated"].S };
              albumList.Add(album);

          });

          return new APIGatewayProxyResponse {
                
              StatusCode = 200,
              Headers = new Dictionary<string, string> () { 
                { "Access-Control-Allow-Origin", "*"},
                { "Access-Control-Allow-Credentials", "true" } },
              Body = JsonConvert.SerializeObject(albumList)
          };
      }

      public async Task<APIGatewayProxyResponse> GetAlbum(APIGatewayProxyRequest req, ILambdaContext context)
      {
        var logger = context.Logger;

          logger.LogLine($"request {JsonConvert.SerializeObject(req)}");
          logger.LogLine($"context {JsonConvert.SerializeObject(context)}");

          var albumsTablename = Environment.GetEnvironmentVariable("ALBUMS_TABLE");
          logger.LogLine($"albumsTablename {albumsTablename}");

          var partition_key = req.PathParameters["PartitionKey"];

         var client = new AmazonDynamoDBClient();

         var db_request = new QueryRequest
          {
              TableName = albumsTablename,
              KeyConditionExpression = "partition_key = :v_Id",
              ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                  {":v_Id", new AttributeValue { S = partition_key }}}
          };

          Album album;
          var result = await client.QueryAsync(db_request);

          if (result.Items.Count > 0)
          {
            logger.LogLine($"album result: [{JsonConvert.SerializeObject(result)}]");
          
            // Get first item
            var item = result.Items[0];
            album = new Album { Name = item["name"].S, 
                                Owner = item["owner"].S,
                                Year = Convert.ToInt32(item["year"].N),
                                Id = new Guid(item["id"].S),
                                DateCreated = item["datecreated"].S };

          }
          else {
            throw new KeyNotFoundException($"No items received for partition_key [{partition_key}]");
          }
      
          return new APIGatewayProxyResponse {
                
              StatusCode = 200,
              Headers = new Dictionary<string, string> () { 
                { "Access-Control-Allow-Origin", "*"},
                { "Access-Control-Allow-Credentials", "true" } },
              Body = JsonConvert.SerializeObject(album)
          };
          

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

          item["partition_key"] = $"{request.Year}_{id}";
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
              Headers = new Dictionary<string, string> () { 
                { "Access-Control-Allow-Origin", "*"},
                { "Access-Control-Allow-Credentials", "true" } },
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


      public async Task<APIGatewayProxyResponse> AddUpload(APIGatewayProxyRequest req, ILambdaContext context)
      {
          var logger = context.Logger;

          logger.LogLine($"request {JsonConvert.SerializeObject(req)}");
          logger.LogLine($"context {JsonConvert.SerializeObject(context)}");

          try {

            var request = JsonConvert.DeserializeObject<AddUploadRequest>(req.Body);

            var albumsTablename = Environment.GetEnvironmentVariable("ALBUMS_TABLE");
            logger.LogLine($"albumsTablename {albumsTablename}");

            var client = new AmazonDynamoDBClient();
            var table = Table.LoadTable(client, albumsTablename);
            var item = new Document();

            var id = Guid.NewGuid();
            var datecreated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff",
                                              CultureInfo.InvariantCulture);

            item["partition_key"] = request.AlbumKey;
            item["sort_key"] = $"upload_{request.OriginalFilename}" ;
            item["filename"] = request.Filename;
            item["original_filename"] = request.OriginalFilename;
            item["last_modified_date"] = request.LastModifiedDate;
            item["owner"] = request.Owner;
            item["size"] = request.Size;
            item["type"] = request.Type;
            item["datecreated"] = datecreated;

            logger.LogLine("Saving doc");
            await table.PutItemAsync(item);
            
            logger.LogLine("Upload created");
          return new APIGatewayProxyResponse {
                  
                StatusCode = 200,
                Headers = new Dictionary<string, string> () { 
                  { "Access-Control-Allow-Origin", "*"},
                  { "Access-Control-Allow-Credentials", "true" } },
                Body = ""
            };
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
          var dstBucket = "ygubbay-photo-albums-thumbnails";
          var fileKey =  HttpUtility.UrlDecode(evnt.Records[0].S3.Object.Key);

        var imageType = System.IO.Path.GetExtension(fileKey).Replace(".", "").ToLower();
        logger.LogLine($"Image type: " + imageType);

        if (imageType != "jpg" && imageType != "png") {
            logger.LogLine($"Uploaded file: {evnt.Records[0].S3.Object.Key}.  Unsupported image type: ${imageType}.  No thumbnail created.");
            return;
        }

       logger.LogLine($"Getting s3client");
        var s3Client = new AmazonS3Client(Amazon.RegionEndpoint.EUWest2);
       logger.LogLine($"try GetObjectAsync: srcBucket [{srcBucket}], fileKey [{fileKey}]");
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
                  putRequest.Key = fileKey;
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
