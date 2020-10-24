using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Amazon.DynamoDBv2.Model;

namespace PhotoAlbum.Models.Responses
{
    public class Album
    {
        public Guid Id {get; set;}
        public string Name {get; set;}
        public int Year {get;set;}
        public string Owner {get;set;}
        public string DateCreated {get;set;}
        public string LastUpdated {get;set;}
        public int PhotoCount {get;set;}

        public string Partition_Key { get { return $"{Year}_{Id}";} }

      public static Album DocToObject(Dictionary<string, AttributeValue> doc)
      {
        return new Album { Name = doc["name"].S, 
                                Owner = doc["owner"].S,
                                Year = Convert.ToInt32(doc["year"].N),
                                Id = new Guid(doc["id"].S),
                                DateCreated = doc["datecreated"].S,
                                LastUpdated = !doc.ContainsKey("last_modified_date") ? "": doc["last_modified_date"].S,
                                PhotoCount = !doc.ContainsKey("photocount") ? 0: Convert.ToInt32(doc["photocount"].N) };
      }

      public static Amazon.DynamoDBv2.DocumentModel.Document ObjectToDoc(Album album)
      {
          var item = new Amazon.DynamoDBv2.DocumentModel.Document();
          
          item["partition_key"] = album.Partition_Key;
          item["sort_key"] = "alb";
          item["owner"] = album.Owner;
          item["year"] = album.Year;
          item["id"] = album.Id;
          item["name"] = album.Name;
          item["datecreated"] = album.DateCreated;
          item["last_modified_date"] = album.LastUpdated;
          item["photocount"] = album.PhotoCount;

          return item;
      }

    }


}