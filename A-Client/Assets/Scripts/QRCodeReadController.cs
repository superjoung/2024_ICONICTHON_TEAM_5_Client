using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using ZXing;

public class QRCodeReadController : MonoBehaviour
{
    [SerializeField] GrpcClient grpcClient;
    [SerializeField] UIController uiController;

    [SerializeField] GameObject cameraUiObject;
    [SerializeField] RawImage cameraTextureRawImage;

    Vector2 requestedRatio;

    WebCamTexture webCamTexture;
    Rect screenRect;

    string qrCodeReadStr;
    string lectureCode;

    int Width;
    int Height;

    private void Start()
    {

        Width = Screen.width;
        Height = Screen.height;

        requestedRatio = new Vector2(Screen.width, Screen.height);
        qrCodeReadStr = "";

        if (webCamTexture) //이미 WebCamTexture가 존재하는 경우에는
        {
            Destroy(webCamTexture); //삭제한다 (메모리 관리)
            webCamTexture = null;
        }

        WebCamDevice[] webCamDevices = WebCamTexture.devices; //현재 접근할 수 있는 카메라 기기들을 모두 가져온다
        if (webCamDevices.Length == 0) return; //만약, 카메라가 없다면 종료

        int backCamIndex = -1; //후방 카메라를 저장하기 위한 변수
        for (int i = 0, l = webCamDevices.Length; i < l; ++i) //카메라를 탐색하면서
        {
            if (!webCamDevices[i].isFrontFacing) //후방 카메라를 발견하면
            {
                backCamIndex = i; //인덱스 저장
                break; //반복문 빠져나오기
            }
        }

        if (backCamIndex != -1) //후방 카메라를 발견했으면
        {
            int requestedWidth = Width; //설정하고자 하는 가로 픽셀 변수 선언 (현재 화면의 가로 픽셀을 기본값으로 지정)
            int requestedHeight = Height; //설정하고자 하는 세로 픽셀 변수 선언 (현재 화면의 세로 픽셀을 기본값으로 지정)
            for (int i = 0, l = webCamDevices[backCamIndex].availableResolutions.Length; i < l; ++i) //현재 선택된 후방 카메라가 활용할 수 있는 해상도를 탐색하면서
            {
                Resolution resolution = webCamDevices[backCamIndex].availableResolutions[i];
                if (GetAspectRatio((int)requestedRatio.x, (int)requestedRatio.y).Equals(GetAspectRatio(resolution.width, resolution.height))) //설정하고자 하는 비율과 일치하는 해상도를 발견하면
                {
                    requestedWidth = resolution.width; //설정하고자 하는 가로 픽셀로 지정
                    requestedHeight = resolution.height; //설정하고자 하는 세로 픽셀로 지정
                    break; //반복문 빠져나오기
                }
            }

            webCamTexture = new WebCamTexture(webCamDevices[backCamIndex].name, requestedWidth, requestedHeight, 60); //카메라 이름으로 WebCamTexture 생성
            webCamTexture.filterMode = FilterMode.Trilinear;
            
        }

        if(webCamTexture == null)
        {
            screenRect = new Rect(0, 0, Screen.width, Screen.height);

            webCamTexture = new WebCamTexture();

            webCamTexture.requestedHeight = Height;

            webCamTexture.requestedWidth = Width;
        }

        webCamTexture.requestedFPS = 60;

        cameraTextureRawImage.texture = webCamTexture;
        //cameraTextureRawImage.material.mainTexture = webCamTexture;
    }

    public void SetLectureCode(string code)
    {
        lectureCode = code;
    }

    private string GetAspectRatio(int width, int height, bool allowPortrait = false) //비율을 반환하는 함수
    {
        if (!allowPortrait && width < height) Swap(ref width, ref height); //세로가 허용되지 않는데, (가로 < 세로)이면 변수값 교환
        float r = (float)width / height; //비율 저장
        return r.ToString("F2"); //소수점 둘째까지 잘라서 문자열로 반환
    }
    private void Swap<T>(ref T a, ref T b) //두 변수값을 교환하는 함수
    {
        T tmp = a;
        a = b;
        b = tmp;
    }

    private void Update()
    {

        if (webCamTexture == null || webCamTexture.isPlaying == false)
            return;

        UpdateWebCamRawImage();

        QRCodeDataUpdate();
    }

    private void UpdateWebCamRawImage() //RawImage를 관리하는 함수
    {
        if (!webCamTexture) return; //WebCamTexture가 존재하지 않으면 종료

        int videoRotAngle = webCamTexture.videoRotationAngle;
        cameraTextureRawImage.transform.localEulerAngles = new Vector3(0, 0, -videoRotAngle); //카메라 회전 각도를 반영

        int width, height;
        if (Screen.orientation == ScreenOrientation.Portrait || Screen.orientation == ScreenOrientation.PortraitUpsideDown) //세로 화면이면
        {
            width = Width; //가로를 고정하고
            height = Width * webCamTexture.width / webCamTexture.height; //WebCamTexture의 비율에 따라 세로를 조절

            // Debug.Log("portrait : height - " + height); 
        }
        else //가로 화면이면
        {
            //Debug.Log("Landspace");
            height = Height; //세로를 고정하고
            width = Height * webCamTexture.width / webCamTexture.height; //WebCamTexture의 비율에 따라 가로를 조절
        }

        if (Mathf.Abs(videoRotAngle) % 180 != 0f) Swap(ref width, ref height); //WebCamTexture 자체가 회전되어있는 경우 가로/세로 값을 교환
        cameraTextureRawImage.rectTransform.sizeDelta = new Vector2(width, height); //RawImage의 size로 지정
    }

    void QRCodeDataUpdate()
    {
        IBarcodeReader barcodeReader = new BarcodeReader();

        var result = barcodeReader.Decode(webCamTexture.GetPixels32(), webCamTexture.width, webCamTexture.height);

        if (result == null)
            return;

        Debug.Log("Read Data : " + result.Text);
        qrCodeReadStr = result.Text;
        uiController.SetQRData(qrCodeReadStr);

        string attendance = grpcClient.SendAttendanceInfo("201911254911", qrCodeReadStr, lectureCode);
        if(attendance != "")
        {
            webCamTexture.Stop();
            cameraUiObject.SetActive(false);
            uiController.ShowAttendancePopup(attendance);
        }

    }

    public void OnClickOpenQRCamera()
    {
        if (webCamTexture == null)
            return;


        cameraUiObject.SetActive(true);
        webCamTexture.Play();
    }
}
