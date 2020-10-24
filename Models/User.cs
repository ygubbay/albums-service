using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Amazon.DynamoDBv2.Model;

namespace PhotoAlbum.Models
{
    public class User
    {
        public Guid Id {get; set;}
        public string Firstname {get; set;}
        public string Lastname {get;set;}
        public string Email {get;set;}
        public string FamilyGroup {get;set;}
        public string DateCreated {get;set;}
        public string LastUpdated {get;set;}
        public string LastLoginDate {get;set;}

      public static Amazon.DynamoDBv2.DocumentModel.Document ObjectToDoc(User user)
      {
          var item = new Amazon.DynamoDBv2.DocumentModel.Document();
          
          item["first_name"] = user.Firstname;
          item["last_name"] = user.Lastname;
          item["email"] = user.Email;
          item["family_group"] = user.FamilyGroup;
          item["date_created"] = user.DateCreated;
          item["last_updated"] = user.LastUpdated;
          item["last_login_date"] = user.LastLoginDate;

          return item;
      }
      public static User DocToObject(Dictionary<string, AttributeValue> doc)
      {
        return new User { Firstname = doc["first_name"].S, 
                            Lastname = doc["last_name"].S,
                            Email = doc["email"].S,
                            FamilyGroup = doc["family_group"].S,
                            DateCreated = doc["date_created"].S,
                            LastUpdated = !doc.ContainsKey("last_updated") ? "": doc["last_updated"].S,
                            LastLoginDate = !doc.ContainsKey("last_login_date") ? "": doc["last_login_date"].S };
      }

    }

 

}