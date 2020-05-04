using System;

namespace PhotoAlbum.Models.Responses
{
    public class CreatePhotoResponse: Album
    {
        public bool IsSuccess {get;set;}
        public string ErrorMessage {get;set;}
    }

}