"""Convex polygon difference for explicit, derived district collision ownership.

The original source models and editable collision are retained. Only a neighbour's
scene instance receives a derived mesh where its upward road overlaps Downtown.
"""
import numpy as np


def clip(poly, a, b, inside=True):
    """Clip an X/Y/Z polygon against the left side of an X/Z edge."""
    def side(p):
        return (b[0]-a[0])*(p[2]-a[2])-(b[2]-a[2])*(p[0]-a[0])
    result=[]
    if not len(poly):
        return result
    previous=poly[-1]; previous_side=side(previous)
    for point in poly:
        point_side=side(point)
        previous_kept=previous_side >= 0 if inside else previous_side <= 0
        kept=point_side >= 0 if inside else point_side <= 0
        if kept != previous_kept:
            fraction=previous_side/(previous_side-point_side)
            result.append(previous+(point-previous)*fraction)
        if kept:
            result.append(point)
        previous=point; previous_side=point_side
    return result


def area(poly):
    if len(poly)<3:
        return 0.
    p=np.asarray(poly)
    return abs(float(np.sum(p[:,0]*np.roll(p[:,2],-1)-p[:,2]*np.roll(p[:,0],-1))))/2


def subtract(poly, triangle):
    triangle=np.asarray(triangle)
    # Left-half-plane clipping requires counterclockwise X/Z orientation.
    if np.cross(triangle[1]-triangle[0],triangle[2]-triangle[0])[1]>0:
        triangle=triangle[::-1]
    intersection=list(poly); outside=[]
    for a,b in zip(triangle,np.roll(triangle,-1,axis=0)):
        piece=clip(intersection,a,b,False)
        if area(piece)>1e-10:
            outside.append(piece)
        intersection=clip(intersection,a,b,True)
        if area(intersection)<1e-10:
            return [poly],0.
    removed=area(intersection)
    return outside,removed
