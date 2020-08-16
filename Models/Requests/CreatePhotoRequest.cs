
using System;

namespace PhotoAlbum.Models.Requests
{
    public class CreatePhotoRequest
    {
        public string AlbumId {get; set;}
        public string Filename {get; set;}
        public string Description {get; set;}
        public int Year {get;set;}
        public string FileKey {get; set;}
        public string ThumbnailKey {get; set;}
        public string Owner {get;set;}
    }
}
