FeatureScript 2200;

// 若 Feature Studio 提示版本不匹配，点击编辑器中的灯泡即可更新到当前工作室的版本。
import(path : "onshape/std/geometry.fs", version : "2200.0");

// ===========================================================================
// Count Teeth —— 识别带轮(pulley) / 链轮(sprocket) / 齿轮(gear) 的齿数
//
// 新方案(只需选一个 part):
//   1. 自动找回转轴: 找所有圆柱面, 聚类同心轴, 选包含最多圆柱面的轴
//      (回转体上有多个同心圆柱面: 内孔、轮毂、齿根圆等)
//   2. 几何中心: bbox 中心投影到轴上
//   3. 标识轴(黄线)和中心(黄点)
//   4. 全局径向采样: 遍历所有边, 按角度分360桶, 每桶取最大半径
//      → "角度-最大半径"曲线的周期性峰值 = 齿数
//      (不依赖边环, 不受减重孔/倒圆干扰, 因为齿顶半径永远最大)
// ===========================================================================

// 点到轴线的径向距离
function radialDistance(point is Vector, axisOrigin is Vector, axisDir is Vector) returns ValueWithUnits
{
    var d = point - axisOrigin;
    return norm(d - dot(d, axisDir) * axisDir);
}

// 点到轴线的垂直距离(用于判断两轴是否同心)
function pointToAxisDistance(point is Vector, axisOrigin is Vector, axisDir is Vector) returns ValueWithUnits
{
    var d = point - axisOrigin;
    return norm(cross(d, axisDir));
}

// 在周期性序列中统计越过阈值的上升沿数量(=齿数)
function countPeaksAbove(samples is array, threshold is ValueWithUnits) returns number
{
    var n = size(samples);
    if (n == 0)
        return 0;
    var count = 0;
    for (var i = 0; i < n; i += 1)
    {
        var prev = samples[(i - 1 + n) % n];
        var cur = samples[i];
        if (prev < threshold && cur >= threshold)
            count += 1;
    }
    return count;
}

annotation { "Feature Type Name" : "Count Teeth", "Feature Type Description" : "Count teeth of a pulley or sprocket" }
export const countTeeth = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Pulley or sprocket part", "Filter" : EntityType.BODY && BodyType.SOLID, "MaxNumberOfPicks" : 1 }
        definition.part is Query;

        annotation { "Name" : "Rename part using tooth count", "Default" : false }
        definition.rename is boolean;

        annotation { "Name" : "Name prefix", "Default" : "Pulley" }
        definition.namePrefix is string;
    }
    {
        // ---------- 1. 自动找回转轴 -----------------------------------------
        // 找所有面, 尝试 evAxis 找圆柱面, 聚类同心轴, 选包含最多圆柱面的轴
        var allFaces = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.FACE));

        var axes = []; // 每个元素 { "origin", "direction" }
        for (var face in allFaces)
        {
            try silent
            {
                var ax = evAxis(context, { "axis" : face });
                axes = append(axes, ax);
            }
        }

        if (size(axes) == 0)
            throw "未找到圆柱面, 无法自动确定回转轴。";

        // 聚类: 对每根轴统计有多少其他轴与它同心(方向平行+原点共线)
        var bestCount = 0;
        var bestAxis = axes[0];
        var tol = 0.01 * meter; // 1cm 同心容差

        for (var i = 0; i < size(axes); i += 1)
        {
            var count = 1;
            for (var j = 0; j < size(axes); j += 1)
            {
                if (i == j) continue;
                if (abs(dot(axes[i].direction, axes[j].direction)) < 0.999) continue;
                if (pointToAxisDistance(axes[i].origin, axes[j].origin, axes[j].direction) < tol)
                    count += 1;
            }
            if (count > bestCount)
            {
                bestCount = count;
                bestAxis = axes[i];
            }
        }

        var axisOrigin is Vector = bestAxis.origin;
        var axisDir is Vector = bestAxis.direction;

        // 几何中心: 用轴原点作为参考点(角度计算只需轴上任意点)
        var centerOnAxis = axisOrigin;

        // ---------- 3. 标识轴和中心 -----------------------------------------
        // 黄色画轴(用线)和中心点
        try silent { debug(context, line(centerOnAxis, axisDir), DebugColor.YELLOW); }
        debug(context, centerOnAxis, DebugColor.YELLOW);

        // ---------- 4. 全局径向采样 -----------------------------------------
        // 遍历所有边, 按角度分360桶, 每桶取最大半径 = 最外围轮廓
        var allEdges = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.EDGE));

        var refU;
        if (abs(axisDir[0]) < 0.9)
            refU = normalize(cross(axisDir, vector(1, 0, 0)));
        else
            refU = normalize(cross(axisDir, vector(0, 1, 0)));
        var refV = cross(axisDir, refU);

        var NUM_BUCKETS = 360;
        var buckets = new box([]);
        for (var b = 0; b < NUM_BUCKETS; b += 1)
            buckets[] = append(buckets[], 0 * meter);

        // 根据边总数调整每边采样数, 总采样约2000点
        var samplesPerEdge = max(4, floor(2000 / size(allEdges)));

        for (var edge in allEdges)
        {
            for (var i = 0; i < samplesPerEdge; i += 1)
            {
                try silent
                {
                    var t = (i + 0.5) / samplesPerEdge;
                    var pt = evEdgeTangentLine(context, { "edge" : edge, "parameter" : t }).origin;
                    var r = radialDistance(pt, axisOrigin, axisDir);
                    var d = pt - axisOrigin;
                    var proj = d - dot(d, axisDir) * axisDir;
                    var angle = atan2(dot(proj, refV), dot(proj, refU));
                    if (angle < 0)
                        angle += 2 * PI;
                    var bucketIdx = floor(angle / (2 * PI) * NUM_BUCKETS);
                    if (bucketIdx >= NUM_BUCKETS)
                        bucketIdx = NUM_BUCKETS - 1;
                    if (r > buckets[][bucketIdx])
                        buckets[][bucketIdx] = r;
                }
            }
        }

        // ---------- 5. 数齿 -------------------------------------------------
        var samples = buckets[]; // 360个半径值, 按角度排列

        var rMax = 0 * meter;
        var rMin = 1e10 * meter;
        for (var s in samples)
        {
            if (s > 0 * meter)
            {
                if (s > rMax) rMax = s;
                if (s < rMin) rMin = s;
            }
        }

        if (rMax <= 0 * meter)
            throw "采样失败, 未获取到有效半径。";

        var toothHeight = rMax - rMin;
        var tipThreshold = rMin + toothHeight * 0.7;
        var teeth = (toothHeight / rMax < 0.005) ? 0 : countPeaksAbove(samples, tipThreshold);

        // 红色标出齿顶方向(在中剖面上画点)
        if (teeth > 0)
        {
            for (var b = 0; b < NUM_BUCKETS; b += 1)
            {
                if (samples[b] >= tipThreshold)
                {
                    var angle = (b + 0.5) / NUM_BUCKETS * 2 * PI;
                    var pt = centerOnAxis
                        + refU * (cos(angle) * samples[b])
                        + refV * (sin(angle) * samples[b]);
                    debug(context, pt, DebugColor.RED);
                }
            }
        }

        // ---------- 6. 输出 -------------------------------------------------
        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | Tip R: " ~ toString(rMax)
            ~ " | Root R: " ~ toString(rMin)
            ~ " | Height: " ~ toString(toothHeight)
            ~ " | Cyl axes: " ~ toString(size(axes))
            ~ " | Concentric: " ~ toString(bestCount)
            ~ " | Edges: " ~ toString(size(allEdges));

        reportFeatureInfo(context, id, diagMsg);
        println(diagMsg);

        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });
