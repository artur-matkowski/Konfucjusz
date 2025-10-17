using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;



public class StudentClass
{
    [Key]
    [Column("stid")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int stid { get; set; }
   //[Column("stname", TypeName = "varchar(n)")]
    public string stname { get; set; } = "";
    //[Column("email", TypeName = "varchar(n)")]
    public string email { get; set; } = "";
}