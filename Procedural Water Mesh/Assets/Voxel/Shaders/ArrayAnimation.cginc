#ifndef ArrayAnimation
#define ArrayAnimation

float SimplePow(float number, uint power)
{
	float sum = 1;
	
	for (uint i = 0; i < power; i++)
	{
		sum *= number;
	}
	
	return sum;

}

int GetDigit(int number, int digitIndex)
{
	return number / (int) SimplePow(10, digitIndex) % 10;
}
    
int GetDigitCount(int number)
{
	return (int) floor(log10(number) + 1);
}

/*

	Data Structure Summary:
	
	int animationData; -> Holds info in digits of the int
	
	animationData = {Animation Speed - how many frames per second - up to 3 digits "00.0"}, 
					{Animation Frame Count - up to 3 digits "000"}

	Example:

	animationData = 015010;
	Animation Speed would be "1.5"
	Animation Frame Count would be "10";

	New Data Format:
	TextureID: 000-000-000

	First 3 digits -> starting Index 0-999
	Middle 3 digits -> total frames 0-999
	Last 3 digits -> speed frames 0-999 -> "99.9"

*/

int GetAnimationFrameCount(int animationData)
{
	//return  ceil(animationData - (animationData / 1000) * 1000);
	
	return GetDigit(animationData, 2) * 100 + GetDigit(animationData, 1) * 10 + GetDigit(animationData, 0);
}

float GetAnimationSpeed(int animationData)
{
	return (animationData / 1000) / 10.0f;
	//return GetDigit(animationData, 5) * 100 + GetDigit(animationData, 4) * 10 + GetDigit(animationData, 3);
}

int GetFrameIndex(int startingIndex, int frameCount, float time, float speed, int arrayLength)
{
	int currentOffsetIndex = floor(time * speed) % frameCount;
	
	int wrappedIndex = (startingIndex + currentOffsetIndex) % arrayLength;
	
	return wrappedIndex;
}

#endif