
using System;

namespace PhotoAlbum.Models.Requests
{
    public class CreateAlbumRequest
    {
        public string Name {get; set;}
        public int Year {get;set;}
        public string Owner {get;set;}
    }
}