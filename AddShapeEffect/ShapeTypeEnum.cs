using System.ComponentModel.DataAnnotations;

namespace AddShapeEffect
{
    internal enum ShapeTypeEnum
    {
        [Display(Name = "背景")]
        背景,
        [Display(Name = "円")]
        円,
        [Display(Name = "三角形")]
        三角形,
        [Display(Name = "四角形")]
        四角形,
        [Display(Name = "五角形")]
        五角形,
        [Display(Name = "六角形")]
        六角形,
        [Display(Name = "星")]
        星,
        [Display(Name = "扇形")]
        扇形,
        [Display(Name = "くさび形")]
        くさび形,
        [Display(Name = "矢印")]
        矢印,
        [Display(Name = "スーパー多角形")]
        スーパー多角形,
        [Display(Name = "集中線")]
        集中線,
        [Display(Name = "タイマー")]
        タイマー,
        [Display(Name = "波形")]
        波形,
        [Display(Name = "線")]
        線,
        [Display(Name = "SVGファイル")]
        SVGファイル,
        [Display(Name = "手書きペン")]
        手書きペン,
        [Display(Name = "数値")]
        数値,
        [Display(Name = "PSDファイル")]
        PSDファイル,
    }
}