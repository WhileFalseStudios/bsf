#pragma once

#include "BsPrerequisites.h"
#include "BsGUILayout.h"

namespace BansheeEngine
{
	class BS_EXPORT GUILayoutX : public GUILayout
	{
	public:
		GUILayoutX() {};
		~GUILayoutX() {};

		void _updateOptimalLayoutSizes();
	protected:
		void _updateLayoutInternal(CM::UINT32 x, CM::UINT32 y, CM::UINT32 width, CM::UINT32 height, CM::Rect clipRect, CM::UINT8 widgetDepth, CM::UINT16 areaDepth);
	};
}