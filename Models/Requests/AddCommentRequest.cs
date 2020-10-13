using System;

namespace PhotoAlbum.Models.Requests
{
    public class AddCommentRequest
    {
        public string AlbumId {get; set;}        
        public string FileKey {get; set;}
        public string Comment {get;set;}
    }
}
