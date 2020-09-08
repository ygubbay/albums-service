using System;
using System.Collections.Generic;

namespace PhotoAlbum.Models.Responses
{
    public class AlbumFull
    {
        public Album Album {get; set;}
        public List<Photo> Photos {get; set;}
    }

}