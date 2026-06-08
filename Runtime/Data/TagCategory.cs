using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// Tag 카테고리 정의 (에디터 그룹핑용). PartTagPresets.Categories에서 사용한다.
    /// </summary>
    public struct TagCategory
    {
        public string categoryName;
        public string[] tags;
        public bool exclusive;       // true면 카테고리 내 1개만 선택 가능 (배타)
        public Color color;          // 카테고리 고유 색상 (에디터 칩/섹션 표시용)

        public TagCategory(string name, string[] tags, bool exclusive = false)
        {
            categoryName = name;
            this.tags = tags;
            this.exclusive = exclusive;
            color = Color.gray;
        }

        public TagCategory(string name, string[] tags, bool exclusive, Color color)
        {
            categoryName = name;
            this.tags = tags;
            this.exclusive = exclusive;
            this.color = color;
        }
    }
}
