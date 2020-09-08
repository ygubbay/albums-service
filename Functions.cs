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
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using System.Collections.Generic;
using System.Linq;
using Amazon.Runtime;
using Amazon.DynamoDBv2.Model;
using System.Web;
using PhotoAlbum.Models;
using Amazon.S3.Encryption;
using Amazon.S3.Model;

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


      public async Task<APIGatewayProxyResponse> GetArchiveAlbums(APIGatewayProxyRequest request, ILambdaContext context)
      {
        var logger = context.Logger;

          logger.LogLine($"request {JsonConvert.SerializeObject(request)}");
          logger.LogLine($"context {JsonConvert.SerializeObject(context)}");

          var archiveTablename = Environment.GetEnvironmentVariable("ARCHIVE_TABLE");
          
          logger.LogLine($"archiveTablename {archiveTablename}");

          
          var client = new AmazonDynamoDBClient();
          var table = Table.LoadTable(client, archiveTablename);

          var db_request = new QueryRequest
          {
              TableName = archiveTablename,
              IndexName = "gsiAlbums",
              KeyConditionExpression = "sort_key = :v_Id",
              ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                  {":v_Id", new AttributeValue { S = "alb" }}}
          };

          var albumList = new List<ArchiveAlbum>();
          var result = await client.QueryAsync(db_request);

          result.Items.ForEach(item => {

              var album = new ArchiveAlbum { Name = item["name"].S, 
                                Owner = item["owner"].S,
                                Year = Convert.ToInt32(item["year"].N),
                                Id = new Guid(item["id"].S),
                                DateCreated = item["datecreated"].S,
                                DateArchived = item["date_archived"].S }; 
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
              KeyConditionExpression = "partition_key = :v_Id AND sort_key = :alb",
              ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                  {":v_Id", new AttributeValue { S = partition_key }},
                  {":alb", new AttributeValue { S = "alb" }}
              }
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



       public async Task<APIGatewayProxyResponse> GetPhoto(APIGatewayProxyRequest req, ILambdaContext context)
      {
        var logger = context.Logger;

          logger.LogLine($"request {JsonConvert.SerializeObject(req)}");
          logger.LogLine($"context {JsonConvert.SerializeObject(context)}");

          var albumsTablename = Environment.GetEnvironmentVariable("ALBUMS_TABLE");

          var album_key = req.PathParameters["PartitionKey"];

         var client = new AmazonDynamoDBClient();

         var conditions = new Dictionary<string, Condition> { 

           // Hash key condition
           {
             "partition_key",
             new Condition { ComparisonOperator = "EQ", 
                             AttributeValueList = new List<AttributeValue> { new AttributeValue { S = album_key } }
             }
           },

           // Range key condition
               {
                    "sort_key", // Reference the correct range key when using indexes
                    new Condition { 
                      ComparisonOperator = "BEGINS_WITH",
                          AttributeValueList = new List<AttributeValue>
                          {
                              new AttributeValue { S = "upload" }
                          }
                    }
                }
         };

          

         var db_request = new QueryRequest
          {
              TableName = albumsTablename,
              KeyConditions = conditions
          };

          List<Photo> photos = new List<Photo>();

          var result = await client.QueryAsync(db_request);
          logger.LogLine($"album results: [{result.Items?.Count}]");

          result.Items.ForEach((ph) => {

            var photo = DbToPhoto(ph);
            photos.Add(photo);
          });
         
      
          return new APIGatewayProxyResponse {
                
              StatusCode = 200,
              Headers = new Dictionary<string, string> () { 
                { "Access-Control-Allow-Origin", "*"},
                { "Access-Control-Allow-Credentials", "true" } },
              Body = JsonConvert.SerializeObject(photos)
          };
      }

      private Photo DbToPhoto(Dictionary<string, AttributeValue> db)
      {
        return new Photo {
               PartitionKey = db["partition_key"].S,
               SortKey = db["sort_key"].S,
               DateCreated = db["datecreated"].S,
               Filename = db["filename"].S,
               LastModifiedDate = !db.ContainsKey("last_modified_date") ? "": db["last_modified_date"].S,
               OriginalFilename = db["original_filename"].S,
               Size = Convert.ToDouble(db["size"].N),
               Type = db["type"].S,
            };
      }



      private async Task<AlbumFull> GetAlbumFull(string albums_table, string album_key)
      {
         var client = new AmazonDynamoDBClient();

         var conditions = new Dictionary<string, Condition> { 

           // Hash key condition
           {
             "partition_key",
             new Condition { ComparisonOperator = "EQ", 
                             AttributeValueList = new List<AttributeValue> { new AttributeValue { S = album_key } }
             }
           }
         };

         var db_request = new QueryRequest
          {
              TableName = albums_table,
              KeyConditions = conditions
          };

          AlbumFull album = new AlbumFull { Photos = new List<Photo>() };
          

          var result = await client.QueryAsync(db_request);
          Console.WriteLine($"album docs: [{result.Items?.Count}]");

          result.Items.ForEach((ph) => {

            if (ph["sort_key"].S == "alb")
            {
              album.Album = new Album { Name = ph["name"].S, 
                                Owner = ph["owner"].S,
                                Year = Convert.ToInt32(ph["year"].N),
                                Id = new Guid(ph["id"].S),
                                DateCreated = ph["datecreated"].S };
            }
            else {
              var photo = new Photo {
                            PartitionKey = ph["partition_key"].S,
                            SortKey = ph["sort_key"].S,
                            DateCreated = ph["datecreated"].S,
                            Filename = ph.ContainsKey("filename") ? ph["filename"].S: "",
                            LastModifiedDate = !ph.ContainsKey("last_modified_date") ? "": ph["last_modified_date"].S,
                            OriginalFilename = !ph.ContainsKey("original_filename") ? "":ph["original_filename"].S,
                            Size = !ph.ContainsKey("size") ? 0:Convert.ToDouble(ph["size"].N),
                            Type = !ph.ContainsKey("type") ? "":ph["type"].S,
                          };

              album.Photos.Add(photo);
            }
            
            
          });

          return album;
      }

      public async Task<APIGatewayProxyResponse> GetPhotos(APIGatewayProxyRequest req, ILambdaContext context)
      {
        var logger = context.Logger;

          logger.LogLine($"request {JsonConvert.SerializeObject(req)}");
          logger.LogLine($"context {JsonConvert.SerializeObject(context)}");

          var albumsTablename = Environment.GetEnvironmentVariable("ALBUMS_TABLE");

          var album_key = req.PathParameters["PartitionKey"];

          var fullAlbum = await GetAlbumFull(albumsTablename, album_key);
         
      
          return new APIGatewayProxyResponse {
                
              StatusCode = 200,
              Headers = new Dictionary<string, string> () { 
                { "Access-Control-Allow-Origin", "*"},
                { "Access-Control-Allow-Credentials", "true" } },
              Body = JsonConvert.SerializeObject(fullAlbum.Photos)
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
                
              StatusCode = 400,
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

          var partition_key = $"{request.Year}_{id}";
          item["partition_key"] = partition_key;
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
              Name = request.Name, 
              Owner = request.Owner,
              Year = request.Year, 
              DateCreated = datecreated,
              PartitionKey = partition_key, 
              IsSuccess = true })
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
            var datecreated = DateTime.UtcNow;

            var item = CreateUploadItem(request, datecreated);

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

      private Document CreateUploadItem(AddUploadRequest request, DateTime date_created)
      {
            var item = new Document();
            
            item["partition_key"] = request.AlbumKey;
            item["sort_key"] = $"upload_{request.OriginalFilename}" ;
            item["filename"] = request.Filename;
            item["original_filename"] = request.OriginalFilename;
            item["last_modified_date"] = request.LastModifiedDate;
            item["owner"] = request.Owner;
            item["size"] = request.Size;
            item["type"] = request.Type;
            item["datecreated"] = date_created.ToString("yyyy-MM-dd HH:mm:ss.fff",
                                                          CultureInfo.InvariantCulture);

            return item;
      }


      private Document CreateUploadItem(Photo photo)
      {
            var item = new Document();
            
            item["partition_key"] = photo.PartitionKey;
            item["sort_key"] = photo.SortKey;
            item["filename"] = photo.Filename;
            item["original_filename"] = photo.OriginalFilename;
            item["last_modified_date"] = photo.LastModifiedDate;
            item["owner"] = photo.Owner;
            item["size"] = photo.Size;
            item["type"] = photo.Type;
            item["datecreated"] = photo.DateCreated;

            return item;
      }

      private Document CreateAlbumItem(Album album)
      {
          var item = new Document();
          
          item["partition_key"] = album.Partition_Key;
          item["sort_key"] = "alb";
          item["owner"] = album.Owner;
          item["year"] = album.Year;
          item["id"] = album.Id;
          item["name"] = album.Name;
          item["datecreated"] = album.DateCreated;

          return item;
      }

      public async Task<APIGatewayProxyResponse> ArchiveAlbum(APIGatewayProxyRequest req, ILambdaContext context)
      {
        var logger = context.Logger;

          logger.LogLine($"request {JsonConvert.SerializeObject(req)}");
          logger.LogLine($"context {JsonConvert.SerializeObject(context)}");

          var albumsTablename = Environment.GetEnvironmentVariable("ALBUMS_TABLE");
          var archiveTable = Environment.GetEnvironmentVariable("ARCHIVE_TABLE");
          logger.LogLine($"albumsTablename {albumsTablename}, archiveTable {archiveTable}");

          var partition_key = req.PathParameters["PartitionKey"];
          logger.LogLine($"ArchiveAlbum: PartitionKey [{partition_key}]");


          // Get existing dynamodb docs for this album
          var fullAlbum = await GetAlbumFull(albumsTablename, partition_key);
          var client = new AmazonDynamoDBClient();
          var table = Table.LoadTable(client, archiveTable);

          // Create dynamodb docs in Archive table
          List<Task> createTasks = new List<Task>();
          fullAlbum.Photos.ForEach(ph => {

                var doc = CreateUploadItem(ph);
                createTasks.Add(table.PutItemAsync(doc));            
          });
          var albumItem = CreateAlbumItem(fullAlbum.Album);
          albumItem["date_archived"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff",
                                            CultureInfo.InvariantCulture);
          createTasks.Add(table.PutItemAsync(albumItem));
          await Task.WhenAll(createTasks);

          // Now delete the dynamodb documents
          List<Task> listOfTasks = new List<Task>();
          fullAlbum.Photos.ForEach(ph => {

              var delRequest = GetDeleteDbRequest(albumsTablename, partition_key, "sort_key", ph.SortKey);
              listOfTasks.Add(client.DeleteItemAsync(delRequest));            
          });
          var delAReq = GetDeleteDbRequest(albumsTablename, partition_key, "sort_key", "alb");
          listOfTasks.Add(client.DeleteItemAsync(delAReq));
          await Task.WhenAll(listOfTasks);

          logger.LogLine("Archive album completed");
          return new APIGatewayProxyResponse {
                
              StatusCode = 200,
              Headers = new Dictionary<string, string> () { 
                { "Access-Control-Allow-Origin", "*"},
                { "Access-Control-Allow-Credentials", "true" } },
              Body = JsonConvert.SerializeObject(new DeleteAlbumResponse { IsSuccess = true, ErrorMessage = "", PartitionKey = partition_key })
          };
      }
          


      public async Task<APIGatewayProxyResponse> DeleteAlbum(APIGatewayProxyRequest req, ILambdaContext context)
      {
        var logger = context.Logger;

          logger.LogLine($"request {JsonConvert.SerializeObject(req)}");
          logger.LogLine($"context {JsonConvert.SerializeObject(context)}");

          var albumsTablename = Environment.GetEnvironmentVariable("ALBUMS_TABLE");
          logger.LogLine($"albumsTablename {albumsTablename}");

          var photosBucket = Environment.GetEnvironmentVariable("PHOTOS_BUCKET");
          var thumbnailsBucket = Environment.GetEnvironmentVariable("THUMBNAILS_BUCKET");

          var partition_key = req.PathParameters["PartitionKey"];
          logger.LogLine($"DeleteAlbum: {partition_key}, photosBucket {photosBucket}, thumbnailsBucket {thumbnailsBucket}");

          // Get key of the album folder
          var s3_folder_key = $"public/{partition_key.Substring(0, 4)}/{partition_key.Substring(5)}/";

          await Deletes3Folder(photosBucket, s3_folder_key);
          await Deletes3Folder(thumbnailsBucket, s3_folder_key);

          // Now delete the dynamodb documents
          var fullAlbum = await GetAlbumFull(albumsTablename, partition_key);

          var client = new AmazonDynamoDBClient();

          
          List<Task> listOfTasks = new List<Task>();
          fullAlbum.Photos.ForEach(ph => {

              var delRequest = GetDeleteDbRequest(albumsTablename, partition_key, "sort_key", ph.SortKey);
              listOfTasks.Add(client.DeleteItemAsync(delRequest));            
          });
          var delCovReq = GetDeleteDbRequest(albumsTablename, partition_key, "sort_key", "alb");
          listOfTasks.Add(client.DeleteItemAsync(delCovReq));

          await Task.WhenAll(listOfTasks);

          logger.LogLine("DeleteAlbum completed");
          return new APIGatewayProxyResponse {
                
              StatusCode = 200,
              Headers = new Dictionary<string, string> () { 
                { "Access-Control-Allow-Origin", "*"},
                { "Access-Control-Allow-Credentials", "true" } },
              Body = JsonConvert.SerializeObject(new DeleteAlbumResponse { IsSuccess = false, ErrorMessage = "", PartitionKey = partition_key })
          };
      }
          
      private DeleteItemRequest GetDeleteDbRequest(string albums_table, string partition_key, string attr_key, string attr_value)
      {
          var key = new Dictionary<string, AttributeValue>
          {
              { "partition_key", new AttributeValue { S = partition_key } },
              { attr_key, new AttributeValue { S = attr_value } }
          };
          return new DeleteItemRequest {

              TableName = albums_table,
              Key = key
          };
      }
      
      private async Task Deletes3Folder(string bucket_name, string key_name)
      {

          Console.WriteLine($"Deletes3Folder: bucket_name [{bucket_name}], key_name [{key_name}]");
          var s3Client = new AmazonS3Client(Amazon.RegionEndpoint.EUWest2);
          
          var listObjectsRequest = new ListObjectsRequest{ BucketName = bucket_name, Prefix = key_name };        
          var s3Objects = await s3Client.ListObjectsAsync(listObjectsRequest);

          // delete the files
          var deleteFilesRequest = new DeleteObjectsRequest {
            BucketName = bucket_name
          };
          
          Console.WriteLine($"s3Objects: {JsonConvert.SerializeObject(s3Objects)}");
          var files = s3Objects.S3Objects.Select(x => x.Key);
          Console.WriteLine($"s3Objects files: {JsonConvert.SerializeObject(files)}");
          
          if (files.Count() > 0) {
            files.ToList().ForEach(x => {

              deleteFilesRequest.AddKey(x);
              Console.WriteLine($"AddKey for delete: {x}");
            });
            var delFilesResponse = await s3Client.DeleteObjectsAsync(deleteFilesRequest);
            Console.WriteLine($"Delete files: Count {JsonConvert.SerializeObject(delFilesResponse)}");
          }
          else {
            Console.WriteLine($"No files in folder: bucket_name [{bucket_name}], key_name [{key_name}]");
          }

          // delete the folder
          var multiObjectDeleteRequest = new DeleteObjectsRequest { 
            BucketName = bucket_name
          };
          multiObjectDeleteRequest.AddKey(key_name);        

          var response = await s3Client.DeleteObjectsAsync(multiObjectDeleteRequest);
          Console.WriteLine("Remove folder: {0} items", response.DeletedObjects.Count);
          
           
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

       public async Task AutoThumbnail(S3Event evnt, ILambdaContext context)
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
