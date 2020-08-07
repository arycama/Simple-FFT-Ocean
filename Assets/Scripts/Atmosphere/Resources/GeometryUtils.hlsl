#ifndef GEOMETRY_UTILS_INCLUDED
#define GEOMETRY_UTILS_INCLUDED

#define PI 3.14159265359

struct Ray
{
	float3 origin;
	float3 direction;
};

struct Plane
{
	float3 position;
	float3 normal;
};

struct RayHit
{
	float3 position;
	float distance;
	bool isValid;
};

struct SphereHit
{
	// Fist hit point
	RayHit hitA;
	
	// Second hit point
	RayHit hitB;
};

struct Bounds
{
	float3 min;
	float3 size;
};

struct Sphere
{
	float3 position;
	float radius;
};

RayHit RayPlaneIntersection(Ray ray, Plane plane)
{
	float denominator = dot(ray.direction, -plane.normal);
	
	RayHit hit;
	hit.distance = dot(plane.position - ray.origin, -plane.normal) / denominator;
	hit.position = ray.origin + ray.direction * hit.distance;
	hit.isValid = denominator > 0;
	
	return hit;
}

float TriangleArea(float2 v0, float2 v1, float2 v2)
{
	return abs(
	v0.x * (v0.y - v1.y) +
	v1.x * (v2.y - v0.y) +
	v2.x * (v0.y - v1.y)
	) / 2;
}

float SquareArea(float2 v0, float2 v1, float2 v2, float2 v3)
{
	return TriangleArea(v0, v1, v2) + TriangleArea(v2, v1, v3);
}

// Returns whether a sphere is within a view frustum defined by 6 planes. (XYZ = plane normal, w = plane distance)
bool SphereFrustumCull(float3 position, float radius, float4 clipPlanes[6])
{
	[unroll]
	for (uint i = 0; i < 6; i++)
	{
		if (dot(float4(position, 1), clipPlanes[i]) < -radius)
		{
			return false;
		}
	}
	
	return true;
}

// Calculates the 8 corners from a bounding box
void BoundingBoxCorners(float3 min, float3 max, out float3 corners[8])
{
	corners[0] = float3(min.x, min.y, min.z);
	corners[1] = float3(min.x, min.y, max.z);
	corners[2] = float3(min.x, max.y, min.z);
	corners[3] = float3(min.x, max.y, max.z);
	corners[4] = float3(max.x, min.y, min.z);
	corners[5] = float3(max.x, min.y, max.z);
	corners[6] = float3(max.x, max.y, min.z);
	corners[7] = float3(max.x, max.y, max.z);
}

void BoundingBoxCorners(Bounds bounds, out float3 corners[8])
{
	BoundingBoxCorners(bounds.min, bounds.min + bounds.size, corners);

}

// Returns whether a clip-space position is within the view frustum.
bool IsClipPosInViewFrustum(float4 clipPos)
{
	return any(clipPos.xyz <= clipPos.w || clipPos.xyz >= -clipPos.w);
}

bool IsClipPosInViewFrustum(float4 clipPos, float radius)
{
	return any((clipPos.xyz + radius) <= clipPos.w || (clipPos.xyz - radius) >= -clipPos.w);
}

bool RaySphereIntersect(float3 center, float radius, float3 rayStart, float3 rayDirection, out float2 distance)
{
	float a = dot(rayDirection, rayDirection);
	float3 oc = rayStart - center;
	float b = 2.0 * dot(oc, rayDirection);
	float c = dot(oc, oc) - radius * radius;
	float discriminant = b * b - 4 * a * c;

	if (discriminant < 0)
	{
		distance = -1.0;
		return false;
	}
	else
	{
		distance = float2(-b - sqrt(discriminant), -b + sqrt(discriminant)) / (2.0 * a);
		return all(distance < 0) ? false : true;
	}
}

SphereHit RaySphereIntersect(Ray ray, Sphere sphere)
{
	float a = dot(ray.direction, ray.direction);
	float3 oc = ray.origin - sphere.position;
	float b = 2.0 * dot(oc, ray.direction);
	float c = dot(oc, oc) - sphere.radius * sphere.radius;
	float discriminant = b * b - 4 * a * c;

	SphereHit hit = (SphereHit)0;
	
	if (discriminant < 0)
	{
		hit.hitA.isValid = hit.hitB.isValid = false;
		hit.hitA.distance = hit.hitB.distance = -1;
	}
	else
	{
		float2 distances = (-b + sqrt(discriminant) * float2(-1, 1)) / (2.0 * a);
		bool2 isValid = distances >= 0;
		
		hit.hitA.distance = distances.x;
		hit.hitB.distance = distances.y;
		
		hit.hitA.isValid = isValid.x;
		hit.hitB.isValid = isValid.y;
		
		hit.hitA.position = ray.origin + ray.direction * distances.x;
		hit.hitB.position = ray.origin + ray.direction * distances.y;
	}
	
	return hit;
}

float2 RaySphereIntersect(float3 center, float radius, float3 rayStart, float3 rayDirection)
{
	float2 dist;
	RaySphereIntersect(center, radius, rayStart, rayDirection, dist);
	return dist;
}

#endif