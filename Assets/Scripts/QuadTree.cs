// Created by Ben Sims 23/07/20

public class QuadTree<T>
{
    public QuadTree<T> lowerLeft, lowerRight, upperLeft, upperRight;

    public T Value { get; }

    public QuadTree<T> this[int index]
    {
        get
        {
            switch (index)
            {
                case 0:
                    return lowerLeft;
                case 1:
                    return lowerRight;
                case 2:
                    return upperLeft;
                case 3:
                    return upperRight;
                default:
                    throw new System.ArgumentOutOfRangeException();
            }
        }
    }

    public QuadTree(T value)
    {
        Value = value;
    }
}