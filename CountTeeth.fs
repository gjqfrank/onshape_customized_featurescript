FeatureScript 3044;

import(path : "onshape/std/geometry.fs", version : "3044.0");

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

// (旧版峰值计数函数已移除, 改用顶点角度聚类)

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
        // 找所有面, 尝试 evAxis 找圆柱面, 收集时去重(方向平行+原点共线视为同一个)
        var allFaces = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.FACE));

        var axes = []; // 去重后的轴列表
        var counts = []; // 每根轴出现的次数
        var tol = 0.01 * meter; // 1cm 同心容差

        for (var face in allFaces)
        {
            try silent
            {
                var ax = evAxis(context, { "axis" : face });
                // 检查是否已有近似轴
                var found = false;
                for (var k = 0; k < size(axes); k += 1)
                {
                    if (abs(dot(axes[k].direction, ax.direction)) > 0.999 &&
                        pointToAxisDistance(axes[k].origin, ax.origin, ax.direction) < tol)
                    {
                        counts[k] += 1;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    axes = append(axes, ax);
                    counts = append(counts, 1);
                }
            }
        }

        if (size(axes) == 0)
            throw "No cylindrical face found, cannot auto-detect rotation axis.";

        // 选出现次数最多的轴(回转轴上有最多同心圆柱面: 内孔、轮毂、齿根圆等)
        var bestCount = counts[0];
        var bestAxis = axes[0];
        for (var k = 1; k < size(axes); k += 1)
        {
            if (counts[k] > bestCount)
            {
                bestCount = counts[k];
                bestAxis = axes[k];
            }
        }

        var axisOrigin is Vector = bestAxis.origin;
        var axisDir is Vector = bestAxis.direction;

        // ---------- 2. 几何中心 ---------------------------------------------
        var bbox = evBox3d(context, { "topology" : definition.part });
        var bboxCenter = (bbox.minCorner + bbox.maxCorner) / 2;
        // 投影到轴上
        var centerOnAxis = axisOrigin + dot(bboxCenter - axisOrigin, axisDir) * axisDir;

        // ---------- 3. 标识轴和中心 -----------------------------------------
        // 黄色画轴(用线)和中心点
        try silent { debug(context, line(centerOnAxis, axisDir), DebugColor.YELLOW); }
        debug(context, centerOnAxis, DebugColor.YELLOW);

        // ---------- 4. 全局径向采样 -----------------------------------------
        var allEdges = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.EDGE));

        if (size(allEdges) == 0)
            throw "No edges found, cannot sample.";

        var refU;
        if (abs(axisDir[0]) < 0.9)
            refU = normalize(cross(axisDir, vector(1, 0, 0)));
        else
            refU = normalize(cross(axisDir, vector(0, 1, 0)));
        var refV = cross(axisDir, refU);

        // 每边采样数: 总采样约1440点, 每边最少8点
        var samplesPerEdge = floor(1440 / size(allEdges));
        if (samplesPerEdge < 8)
            samplesPerEdge = 8;

        // 第一遍: 收集所有采样点, 找全局最大半径
        var allSampleData = [];
        var globalMaxR = 0 * meter;
        for (var edge in allEdges)
        {
            for (var i = 0; i < samplesPerEdge; i += 1)
            {
                var t = (i + 0.5) / samplesPerEdge;
                try silent
                {
                    var pt = evEdgeTangentLine(context, { "edge" : edge, "parameter" : t }).origin;
                    var r = radialDistance(pt, axisOrigin, axisDir);
                    var d = pt - axisOrigin;
                    var proj = d - dot(d, axisDir) * axisDir;
                    var angle = atan2(dot(proj, refV), dot(proj, refU));
                    if (angle < 0)
                        angle += 2 * PI;
                    allSampleData = append(allSampleData, { "angle" : angle, "radius" : r });
                    if (r > globalMaxR) globalMaxR = r;
                }
            }
        }

        if (size(allSampleData) < 4)
            throw "Sampling failed. Samples: " ~ toString(size(allSampleData)) ~ " / Edges: " ~ toString(size(allEdges));

        // 第二遍: 过滤非齿廓采样点(只保留半径 > 50% maxR, 排除减重孔/内孔)
        var sampleData = [];
        for (var sd in allSampleData)
        {
            if (sd.radius > globalMaxR * 0.5)
                sampleData = append(sampleData, sd);
        }

        if (size(sampleData) < 4)
            throw "No tooth-area samples after filtering. GlobalMaxR: " ~ toString(globalMaxR);

        // ---------- 5. 顶点角度聚类数齿 ----------------------------------
        // 不用边采样的峰值计数(噪声太大, 且只覆盖半圈)
        // 改用顶点聚类: 找齿尖顶点, 按角度聚类, 簇数=齿数
        var allVertices = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.VERTEX));

        // 收集所有顶点的(角度, 半径)
        var vertexData = [];
        var vertexMaxR = 0 * meter;
        for (var v in allVertices)
        {
            try silent
            {
                var pt = evVertexPoint(context, { "vertex" : v });
                var r = radialDistance(pt, axisOrigin, axisDir);
                var d = pt - axisOrigin;
                var proj = d - dot(d, axisDir) * axisDir;
                // atan2返回带单位的弧度, 除以radian得到纯数字, 再转度数便于比较
                var angle = atan2(dot(proj, refV), dot(proj, refU)) / degree;
                if (angle < 0)
                    angle += 360;
                vertexData = append(vertexData, { "angle" : angle, "radius" : r, "point" : pt });
                if (r > vertexMaxR) vertexMaxR = r;
            }
        }

        // 过滤: 只保留半径 > 98% maxR 的顶点(真齿尖顶点, 排除fillet/齿根)
        var tipVertices = [];
        for (var vd in vertexData)
        {
            if (vd.radius > vertexMaxR * 0.98)
                tipVertices = append(tipVertices, vd);
        }

        // 按角度排序
        tipVertices = sort(tipVertices, function(a, b)
        {
            return a.angle - b.angle;
        });

        // 聚类: 簇数 = gap > 阈值的次数(环形, 含首尾wrap)
        // 修正: 旧逻辑在密集覆盖时首尾被错误减1导致teeth=0
        var teeth = 0;
        if (size(tipVertices) > 0)
        {
            var clusterThreshold = 5; // 5度
            // 数所有内部gap > 阈值的次数
            for (var i = 1; i < size(tipVertices); i += 1)
            {
                var gap = tipVertices[i].angle - tipVertices[i - 1].angle;
                if (gap > clusterThreshold)
                    teeth += 1;
            }
            // 检查首尾wrap gap
            var wrapGap = (360 - tipVertices[size(tipVertices) - 1].angle) + tipVertices[0].angle;
            if (wrapGap > clusterThreshold)
                teeth += 1;
            // 如果没有任何gap(所有点在一个簇里), teeth=0, 修正为1
            if (teeth == 0)
                teeth = 1;
        }

        // 红色标出齿尖顶点
        for (var vd in tipVertices)
        {
            debug(context, vd.point, DebugColor.RED);
        }

        // 边采样仅用于找全局maxR(辅助), 齿数由顶点聚类决定
        var _ = globalMaxR; // 避免unused警告

        // ---------- 6. 输出 -------------------------------------------------
        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | Tip vertices: " ~ toString(size(tipVertices))
            ~ " | Total vertices: " ~ toString(size(vertexData))
            ~ " | VMaxR: " ~ toString(vertexMaxR)
            ~ " | GlobalMaxR: " ~ toString(globalMaxR)
            ~ " | Edges: " ~ toString(size(allEdges));

        reportFeatureInfo(context, id, diagMsg);
        println(diagMsg);

        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });
