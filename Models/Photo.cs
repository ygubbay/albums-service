using System;

namespace PhotoAlbum.Models
{
    public class Photo
    {
        public string PartitionKey {get; set;}
        public string SortKey {get; set;}
        public string Filename {get;set;}
        public string DateCreated {get;set;}
        public string LastModifiedDate {get;set;}
        public string OriginalFilename {get;set;}
        public double Size { get;set;}
        public string Type {get;set;}
        public string Owner {get;set;}
        public string Comment {get;set;}
    }

}
