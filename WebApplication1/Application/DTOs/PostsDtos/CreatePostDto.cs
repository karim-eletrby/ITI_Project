using Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Application.DTOs.PostsDtos
{
    public record CreatePostDto(
     [MaxLength(5000)] 
        string Content,
     [MaxLength(500)] 
        string? MediaUrl,
     PostPrivacy Privacy = PostPrivacy.Public
 );
}
