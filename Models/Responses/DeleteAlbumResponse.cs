
using System;

namespace PhotoAlbum.Models.Responses
{
    public class DeleteAlbumResponse
    {
        public bool IsSuccess {get;set;}
        public string ErrorMessage {get;set;}
        public string PartitionKey {get;set;}
    }
}