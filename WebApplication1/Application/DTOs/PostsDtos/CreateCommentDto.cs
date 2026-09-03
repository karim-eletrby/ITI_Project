using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Application.DTOs.PostsDtos
{
    public record CreateCommentDto(
    [Required, MaxLength(1000)] 
    string Content,
    int? ParentCommentId = null
);
}
