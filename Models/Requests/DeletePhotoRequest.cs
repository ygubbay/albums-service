using System;

namespace PhotoAlbum.Models.Requests
{
    public class DeletePhotoRequest
    {
        public string AlbumId {get; set;}        
        public string FileKey {get; set;}
    }
}
