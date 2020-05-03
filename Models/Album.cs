using System;

namespace PhotoAlbum.Models.Responses
{
    public class Album
    {

        public Guid Id {get; set;}
        public string Name {get; set;}
        public int Year {get;set;}
        public string Owner {get;set;}
    }

}