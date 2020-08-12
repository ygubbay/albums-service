using System;

namespace PhotoAlbum.Models.Requests
{
    public class AddUploadRequest
    {
        public string AlbumKey {get;set;}
        public string LastModifiedDate {get; set;}
        public string Filename {get;set;}
        public string OriginalFilename {get;set;}
        public double Size {get;set;}
        public string Type {get;set;}
        public string Owner {get;set;}
    }
}
