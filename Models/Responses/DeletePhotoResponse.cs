
using System;

namespace PhotoAlbum.Models.Responses
{
    public class DeletePhotoResponse
    {
        public bool IsSuccess {get;set;}
        public string ErrorMessage {get;set;}
        public string AlbumId {get; set;}        
        public string FileKey {get; set;}
    }
}