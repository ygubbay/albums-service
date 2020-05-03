using System;

namespace PhotoAlbum.Models.Responses
{
    public class CreateAlbumResponse: Album
    {
        public bool IsSuccess {get;set;}
        public string ErrorMessage {get;set;}
    }

}